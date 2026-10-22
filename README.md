# Accantona — Mental Accounting

Webapp personale per un accantonamento virtuale in euro: movimenti, tassi storici, imposte per maturazione, accrediti automatici e simulazioni. Un solo account, nessuna registrazione o connessione bancaria.

## Documentazione
- [Specifiche funzionali](docs/specifiche-funzionali.md)
- [Architettura](docs/architettura.md)
- [Verifiche e limiti dell'ambiente](docs/verifica.md)

## Sviluppo
Richiede SDK .NET 10, Node.js 24 LTS (o versione compatibile successiva) con npm e PostgreSQL 17 (anche locale). Configurare `ConnectionStrings__Accounting` nell'ambiente, senza mettere password nei file versionati.

```sh
dotnet restore
dotnet tool restore
dotnet build --no-restore
dotnet ef migrations list --project src/MentalAccounting.Infrastructure --startup-project src/MentalAccounting.Web
dotnet run --project src/MentalAccounting.Web -- migrate
dotnet run --project src/MentalAccounting.Web -- create-user
dotnet run --project src/MentalAccounting.Web --launch-profile https
dotnet test
```

`create-user` crea l'unico account (nome predefinito `admin`, configurabile con `ADMIN_USERNAME`), chiedendo la password senza mostrarla. `reset-password` reimposta la password e sblocca l'account. È possibile fornire `ADMIN_PASSWORD` tramite ambiente per automazione, rimuovendola subito dopo. Nessun servizio email richiesto. Usare HTTPS anche in sviluppo per i cookie Secure.

## Interfaccia e sviluppo degli stili

L'interfaccia usa Tailwind CSS, senza Bootstrap, jQuery o dipendenze CDN. Il selettore Sistema/Chiaro/Scuro è disponibile anche nel login; la preferenza viene ricordata solo nel browser del dispositivo.

`dotnet build`, `dotnet run` e `dotnet publish` installano automaticamente le dipendenze npm con `npm ci` quando necessario e compilano `Styles/app.css` in `wwwroot/css/site.css`. Il CSS compilato è versionato per renderne verificabile l'output; modificare il sorgente in `Styles`, non il file generato. Gli script dei componenti sono in `wwwroot/js`. Il lockfile npm rende riproducibile l'installazione.

Durante le modifiche agli stili, in un secondo terminale:

```powershell
cd src/MentalAccounting.Web
npm.cmd run css:watch
```

Per i test JavaScript e la compilazione manuale:

```powershell
cd src/MentalAccounting.Web
npm.cmd ci
npm.cmd test
npm.cmd run css:build
```

Su Linux/macOS usare `npm` al posto di `npm.cmd`. Nel Dockerfile uno stage Node compila il CSS prima della pubblicazione .NET; il container finale non contiene Node. `SkipUiBuild=true` è riservato a pipeline che hanno già prodotto il CSS.

Le griglie offrono filtri per colonna, ordinamento e pagine da 10/25/50 righe. Il pulsante Filtri per colonna espone i controlli; Azzera filtri ripristina l'intero insieme. I riepiloghi e le esportazioni restano complessivi. Nelle griglie modificabili tutte le righe non eliminate vengono salvate, anche quando nascoste dai filtri o dalla paginazione. Le modifiche restano soggette ad anteprima e conferma. Le percentuali accettano la virgola italiana; non sono più necessarie righe di testo separate da punto e virgola.

## Pubblicazione su Linux

Docker Engine con Compose plugin, reverse proxy HTTPS già disponibile e hostname sono prerequisiti. Nessuna porta PostgreSQL pubblica.

```sh
cp deploy/.env.example deploy/.env
chmod 600 deploy/.env
# Modificare password, hostname e IP del proxy fidato.
docker compose -f deploy/compose.yaml build
docker compose -f deploy/compose.yaml up -d db
docker compose -f deploy/compose.yaml run --rm web migrate
docker compose -f deploy/compose.yaml run --rm web create-user
docker compose -f deploy/compose.yaml up -d
```

La porta web è esposta su loopback. Configurare il proxy con [l'esempio Nginx](deploy/nginx.conf.example). `TRUSTED_PROXY_IP` deve coincidere con l'IP sorgente visto dal container: su Docker può essere l'IP del gateway della rete frontend, ricavabile con `docker network inspect mental-accounting_frontend`. Non usare una rete fidata universale. Per accesso VPN usare lo stesso hostname HTTPS tramite proxy; nessun bypass del login. `/health` restituisce salute database senza dettagli. Verificare prima del rilascio login/logout, cookie Secure, redirect HTTPS e impossibilità di falsificare gli header inoltrati.

Per aggiornare: eseguire backup, fermare web, ricostruire l'immagine, applicare `migrate` e riavviare. Le migrazioni non partono automaticamente. In caso di errore mantenere web fermo e ripristinare backup e immagine precedente.

## Backup e ripristino

Lo script richiede Linux con `flock`, `find` e Docker Compose. Eseguirlo prima delle migrazioni e ogni giorno tramite cron, per esempio:

```cron
15 3 * * * /bin/sh /opt/mental-accounting/deploy/backup/backup.sh >> /var/log/mental-accounting-backup.log 2>&1
```

Conserva le ultime 30 copie riuscite in `deploy/backups`, protette da permessi. Il dump include dati e account Identity; la schermata Dati esporta invece un JSON senza credenziali. Copiare regolarmente i backup su un altro dispositivo. Le chiavi Data Protection sono nel volume `keys`; salvarle separatamente con permessi riservati per conservare sessioni e anteprime dopo un recupero del server.

```sh
sh deploy/backup/restore.sh /percorso/accounting-YYYYMMDDTHHMMSSZ.dump --confirm-replace
```

Il ripristino ferma web e sostituisce il database in un'unica transazione. Se fallisce web rimane fermo. Per provare su database vuoto usare un'installazione Compose separata (altro nome progetto, porta e volumi), avviare db, ripristinare il dump e avviare la stessa versione dell'app; verificare login e confronto dei saldi prima di considerare valido il backup. Non usare mai i dati reali come database di test automatici.

## Test

I test unitari e HTTP non richiedono PostgreSQL. Per abilitare i test PostgreSQL impostare `TEST_POSTGRES` alla connessione di un database **dedicato e vuoto**, il cui nome deve terminare in `_test`. I test applicano migrazioni e ricreano i dati delle tabelle applicative. Senza questa variabile i test database risultano esplicitamente saltati. L'importazione del foglio reale viene verificata solo se `TEST_WORKBOOK` indica il file locale; il file personale non viene incluso nel repository.
