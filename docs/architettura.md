# Architettura

## Stack
.NET 10 LTS, ASP.NET Core Razor Pages, Bootstrap locale, EF Core/Npgsql, PostgreSQL, ASP.NET Core Identity, ClosedXML per lettura XLSX, xUnit. Un'applicazione server, nessuna API pubblica o frontend separato. Docker Compose su Linux dietro reverse proxy esterno HTTPS.

## Dipendenze
- Domain: record contabili, calendario e motore deterministico, solo libreria standard.
- Application → Domain: validazione, anteprime, scritture, scenari, import/export e contratti di persistenza.
- Infrastructure → Application/Domain: EF Core, Identity, importazione XLSX, transazioni.
- Web → Application/Infrastructure: pagine e configurazione.

Decimal per calcoli e valori; DateOnly per date contabili; UTC per audit. Il singolo accantonamento è un aggregato JSONB versionato in PostgreSQL: struttura C# tipizzata, decimali JSON senza conversione a double, nessun calcolo monetario SQL. Identity usa tabelle relazionali. Gli accrediti sono risultati derivati e non vengono salvati come movimenti. Il salvataggio atomico usa versione ottimistica e transazione; ogni anteprima è confermata confrontando la versione originale.

## Distribuzione
Database solo su rete Docker interna; app pubblicata su loopback del server per reverse proxy. TLS termina sul proxy; cookie sempre Secure. Configurare IP di proxy fidati esplicitamente, inclusi gli IP effettivi dopo NAT Docker. Chiavi Data Protection persistenti. Account unico creato/reimpostato da CLI, nessuna registrazione. Login con blocco temporaneo e rate limit; antiforgery nei moduli. Nessun segreto nel repository.

Migrazioni esplicite mediante comando `migrate`, non all'avvio. Backup pg_dump giornaliero, 30 copie, procedura di ripristino nel README. Health check database senza dettagli pubblici. Log senza dati contabili. Scenari e ripristini passano dallo stesso validatore dello storico.

## Verifica
Test unitari del calendario e di esempi numerici, ricalcolo, segni, tassazione, bisestili e arrotondamenti; test applicativi per conflitti e isolamento; integrazione PostgreSQL e test HTTP autenticazione/antiforgery. I test PostgreSQL richiedono esplicitamente un database di test dedicato. Controllo manuale su viewport mobile, dietro proxy HTTPS, backup e ripristino prima della pubblicazione.
