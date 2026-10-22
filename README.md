# Accantona — Mental Accounting

Webapp personale per un accantonamento virtuale in euro: movimenti, tassi storici, imposte per maturazione, accrediti automatici e simulazioni. Un solo account, nessuna registrazione o connessione bancaria.

## Documentazione
- [Specifiche funzionali](docs/specifiche-funzionali.md)
- [Architettura](docs/architettura.md)
- [Verifiche e limiti dell'ambiente](docs/verifica.md)

## Sviluppo
Richiede SDK .NET 10 e PostgreSQL 17 (anche locale). Configurare `ConnectionStrings__Accounting` nell'ambiente, senza mettere password nei file versionati.

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
