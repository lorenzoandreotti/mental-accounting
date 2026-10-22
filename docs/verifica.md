# Verifica dell'implementazione

Verifiche eseguite il 22 ottobre 2026 con SDK .NET 10.0.402 e PostgreSQL 17.11 temporaneo, limitato a loopback. Nessun servizio di sistema installato e nessun dato importato automaticamente nell'applicazione.

## Eseguite
- Compilazione completa e pubblicazione Release.
- 32 test: 17 di dominio, 6 applicativi, 9 di integrazione, inclusi tutti i test PostgreSQL e lettura del foglio personale.
- Esempi indipendenti: variazioni di saldo/tasso/aliquota, basi annue, bisestile, chiusura, pagamento differito, capitalizzazione netta e calcolo composto con decimali.
- Anteprima e conferma HTTP, cultura italiana, salvataggio di movimenti e scenari, ripristino JSON senza modifiche prima della conferma.
- Login reale Identity, cookie Secure/HttpOnly, logout, blocco dopo tentativi falliti e antiforgery.
- Header HTTPS accettati solo dal proxy esplicitamente fidato; header di un'altra origine non evitano il redirect HTTPS.
- Migrazione su PostgreSQL, persistenza esatta dei decimali, versione ottimistica e assenza di modifiche al modello non migrate.
- `pg_dump` e `pg_restore` su database vuoto: confronto del contenuto dell'accantonamento identico prima e dopo il ripristino.
- Sintassi degli script shell backup/ripristino verificata con `bash -n`.
- Cache, dati temporanei, credenziali, chiavi e backup esclusi da Git.

## Da eseguire sulla destinazione
- Build e avvio Docker Compose: Docker non disponibile nell'ambiente di implementazione.
- Controllo visivo desktop/mobile: nessun browser collegato agli strumenti disponibili. Le pagine sono compilate e verificate via richieste HTTP, non tramite screenshot.
- Certificato pubblico, IP effettivo del reverse proxy dopo NAT, hostname, cron e copia dei backup fuori dal server.
- Ripetere una prova di ripristino con i volumi e il reverse proxy della destinazione prima della pubblicazione.

Il foglio originale non è stato modificato né incluso nel repository. Nell'importazione vengono riconosciuti e segnalati i prelievi negativi; gli interessi e consolidamenti del foglio non vengono usati come risultati corretti.

## Aggiornamento interfaccia Tailwind

- Bootstrap e jQuery rimossi; pagine convertite a Tailwind CSS compilato localmente con lockfile npm.
- Tema Sistema/Chiaro/Scuro applicato nel `head`, con preferenza locale e funzionamento anche senza accesso allo storage.
- Griglie per tutte le tabelle, inclusi intero storico in Panoramica, anteprime, condizioni e ipotesi delle simulazioni. Filtri prima della paginazione, ordinamento numerico esatto e identificativi stabili dei moduli.
- **37 test .NET superati**: 17 di dominio, 6 applicativi, 14 di integrazione. Inclusi PostgreSQL sul solo database dedicato `accounting_test`, Identity e lettura del foglio personale.
- **11 test JavaScript Vitest/jsdom superati**: tema, ordinamento date/numeri, filtri combinati, paginazione, aggiunta/rimozione, invio di righe nascoste e focus su righe modificate/non valide.
- Test HTTP aggiuntivi: 13 condizioni salvate dopo conferma, percentuali italiane, righe incomplete, decorrenze duplicate e mantenimento dei valori in caso di errore. Panoramica ed esportazione comprendono più di otto movimenti.
- Pubblicazione Release riuscita con CSS e moduli JavaScript inclusi. Nessuna migrazione o modifica al formato dei backup.
- Le verifiche sono state eseguite in Release perché la copia Debug dell'utente era in esecuzione; nessun processo dell'utente è stato fermato.
- Il controllo visivo desktop/mobile e da tastiera nel browser resta da eseguire: nessun browser è collegato agli strumenti disponibili. I test DOM non sostituiscono il controllo visivo. Anche la build Docker resta da verificare su una macchina con Docker disponibile.
