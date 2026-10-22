# Prova locale dello stack Docker

Lo stack contiene soltanto webapp e PostgreSQL. HTTPS e reverse proxy sono gestiti esternamente a Docker. La webapp pubblica HTTP esclusivamente su `127.0.0.1:8080`; il database non pubblica porte. Il progetto `mental-accounting-smoke` mantiene dati e chiavi separati dal PostgreSQL avviato dai binari.

## Configurazione

Il file locale `.local/docker-smoke/stack.env`, ignorato da Git e dalla build Docker, contiene:

- `POSTGRES_PASSWORD`: password casuale del database di prova, gia' generata.
- `HTTP_PORT=8080`: porta HTTP di destinazione del proxy sull'host.
- `ALLOWED_HOSTS`: hostname HTTPS usato dal browser, senza schema o porta (ad esempio `localhost`).
- `TRUSTED_PROXY_IP`: indirizzo sorgente del proxy come visto dal container web; non necessariamente 127.0.0.1, specialmente su Docker Desktop. Verificarlo sull'ambiente in cui gira il proxy, senza fidarsi di intere reti.

Configurare il reverse proxy esterno con certificato e hostname propri, inoltrando verso `http://127.0.0.1:8080`. Inoltrare Host originale, X-Forwarded-Proto e X-Forwarded-For, sovrascrivendo nel proxy gli header forniti dal client. Se il proxy gira su un'altra macchina, la pubblicazione su loopback richiede un collegamento dedicato oppure una modifica esplicita dell'indirizzo di ascolto.

L'applicazione richiede HTTPS per login e cookie. La porta 8080 e' l'upstream del proxy, non l'indirizzo da usare per accedere normalmente dal browser. Certificati e chiavi TLS non vengono montati nei container dello stack. Se il proxy espone una porta HTTPS diversa da 443, configurare anche `ASPNETCORE_HTTPS_PORT` del servizio web tramite un override locale.

La connessione PostgreSQL usa password e disabilita GSS/Kerberos, non utilizzato dallo stack.

## Comandi PowerShell

Dalla radice del repository:

```powershell
$compose = @('--env-file', '.local/docker-smoke/stack.env',
    '-p', 'mental-accounting-smoke', '-f', 'deploy/compose.yaml')

docker compose @compose config --quiet
docker compose @compose build web
docker compose @compose up -d --wait db
docker compose @compose run --rm web migrate
# Solo alla prima installazione, se l'account non esiste:
docker compose @compose run --rm web create-user
docker compose @compose up -d
docker compose @compose ps
```

`create-user` chiede la password interattivamente senza visualizzarla. Account predefinito `admin`; almeno 12 caratteri con maiuscole, minuscole, numeri e simboli. Per reimpostare un account esistente: `docker compose @compose run --rm web reset-password`.

Dopo avere predisposto il proxy esterno, aprire il relativo indirizzo HTTPS e controllare `/health`, login/logout e le funzioni dell'applicazione. Database e chiavi dei cookie persistono nei volumi `mental-accounting-smoke_database` e `mental-accounting-smoke_keys`.

```powershell
docker compose @compose logs --tail 100 web db
docker compose @compose stop
docker compose @compose up -d
```

`stop` conserva i dati. Non usare `down -v` per una normale fermata. Gli script `deploy/backup/*.sh` sono destinati al server Linux e al suo progetto Compose: per questa prova usare sempre gli stessi argomenti `@compose` e un secondo progetto isolato per testare il ripristino.

## Verifiche del 22 ottobre 2026

Build multistage e migrazione iniziale riuscite. Prima della rimozione del proxy container erano stati verificati HTTPS, health e pagina di login. Questi controlli andranno ripetuti attraverso il proxy esterno effettivo. I dati del database Docker e quelli del PostgreSQL avviato dai binari restano separati.

Dopo la rimozione: stack avviato con soli web e db, volumi conservati, health Healthy simulando gli header del proxy e HTTP diretto reindirizzato a HTTPS (307). Per le connessioni dall'host e' stato osservato `172.19.0.1`, configurato nel file locale come proxy fidato. L'indirizzo dipende dalla rete Docker creata: ricontrollarlo dopo ricreazioni della rete o spostamenti su un altro host. Il precedente accesso HTTPS sulla porta 8443 non e' piu' fornito dallo stack.
