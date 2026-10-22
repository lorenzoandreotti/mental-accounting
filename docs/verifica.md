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

## Storico del saldo e riorganizzazione

- Pagina iniziale Movimenti con modulo espandibile, riepiloghi, grafico giornaliero e cronologia con accrediti automatici di sola lettura. Impostazioni suddivise in Condizioni, Aspetto e Importa/Esporta.
- Storico prodotto nello stesso ciclo del calcolo, opzionale per gli altri chiamanti; ultimo punto riconciliato con il riepilogo, compresi pagamenti differiti e chiusure a fine giornata. Nessuna migrazione database o modifica ai backup.
- Grafico Chart.js compilato localmente con esbuild, caricato solo quando presente, con periodo indipendente, tooltip e lettura accessibile per data. Palette aggiornata al cambio tema.
- Tema Automatico/Manuale compatibile con le preferenze preesistenti, toggle nascosto in Automatico prima del rendering e visibile in Manuale anche nel login.
- **50 test .NET superati**: 24 dominio, 7 applicativi, 19 integrazione. Inclusi PostgreSQL sul database separato `accounting_test`, Identity e lettura del foglio originale. Verificati redirect legacy con parametri di download, modulo aperto su modifica/errori, accrediti non modificabili e selezione del periodo di calcolo.
- **16 test JavaScript superati**: 8 griglie, 4 tema, 4 grafico. Verificati intervalli inclusivi, febbraio bisestile, indipendenza dai filtri e aggiornamento dei colori.
- Pubblicazione Release riuscita con CSS e bundle del grafico inclusi; pipeline Docker aggiornata per copiare entrambi gli asset.
- Nessun processo dell'utente o PostgreSQL fermato durante questo intervento. Controllo browser riprovato: nessuna superficie browser disponibile; verifica visiva e build Docker restano da eseguire sulla destinazione.


## Modal dei movimenti (22 ottobre 2026)

Inserimento e modifica salvano direttamente dal dialog; cancellazione con anteprima server e confronto dei riepiloghi nel dialog, ticket dedicato e conferma esplicita. Nessuna migrazione o modifica ai backup.

- Suite .NET Release: 55 test superati (24 dominio, 7 applicazione, 24 integrazione), nessuno saltato. Inclusi test PostgreSQL sul solo database dedicato accounting_test e lettura del foglio originale senza modifiche.
- JavaScript: 20 test superati, inclusi apertura/chiusura, focus, compilazione, conservazione errori e invii ripetuti dei modal. Escape verificato come comportamento nativo non intercettato; jsdom usa uno stub delle API dialog.
- Asset: npm run ui:build riuscito; esbuild richiede esecuzione fuori dalla sandbox locale per leggere i percorsi degli antenati del progetto.
- Pubblicazione Release riuscita in .local/publish-modal con asset precompilati (SkipUiBuild=true).
- Browser: nessuna superficie disponibile nel controllo UI. Restano da verificare visivamente temi, focus trap nativo e viewport desktop/mobile in un browser reale.
- PostgreSQL trovato attivo su 127.0.0.1:55432; nessun processo dell'utente avviato o fermato.
