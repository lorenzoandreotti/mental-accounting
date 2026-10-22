# Architettura

## Stack
.NET 10 LTS, ASP.NET Core Razor Pages, Tailwind CSS compilato localmente, EF Core/Npgsql, PostgreSQL, ASP.NET Core Identity, ClosedXML per lettura XLSX, xUnit. Un'applicazione server, nessuna API pubblica o frontend separato. Docker Compose su Linux dietro reverse proxy esterno HTTPS.

La build UI usa npm/CLI Tailwind ed esbuild con lockfile e target MSBuild. Lo stage Node nel Dockerfile è separato dal runtime .NET. Componenti di stile condivisi in `Styles/app.css`; JavaScript ES modules per griglie, menu responsive e tema applicato prima del rendering. Il grafico Chart.js viene compilato in un bundle locale con i soli componenti necessari e caricato soltanto nella pagina Movimenti. Nessun framework frontend o CDN. Vitest/jsdom verifica i comportamenti del DOM.

## Storico e navigazione
`Calculate` espone il parametro opzionale `includeHistory` (default false). Il motore raccoglie `BalanceHistoryPoint` nello stesso ciclo giornaliero e restituisce `CalculationResult.History`; ogni punto contiene disponibile, netto maturato e totale a fine giornata. Gli importi chiusi non pagati restano nel maturato fino al pagamento. Il grafico non ricalcola interessi o saldi nel browser e non introduce persistenza aggiuntiva.

`AccountActivity.Build` combina movimenti manuali con le sole liquidazioni accreditate, mantenendo identificativi di modifica soltanto per i primi. La pagina iniziale gestisce inserimento e modifica in un dialog nativo con POST diretto a Commit. La cancellazione usa anteprima server e conferma in un dialog separato, con ticket protetto tramite purpose dedicato DeleteMovement.v1 e scadenza di 30 minuti. La versione originale resta vincolante anche dopo errori; i conflitti richiedono ricaricamento esplicito. Il modulo JavaScript movement-modal.js gestisce focus, apertura/chiusura e blocco degli invii ripetuti senza API aggiuntive. Le precedenti pagine Movimenti, Impostazioni e Dati diventano redirect locali con conservazione della query; i POST legacy vengono inoltrati mantenendo il metodo. Condizioni, Aspetto e Dati vivono sotto `/Settings/`. Il dettaglio interessi può selezionare un singolo periodo, mantenendo lo stato effettivo di accredito alla data odierna.

Il tema rimane una preferenza locale: `system` indica Automatica e nasconde il toggle; `light` e `dark` indicano Manuale. L'evento `accantona:themechange` aggiorna anche la palette del grafico senza cambiarne periodo o dati.

Le righe dei moduli sono collezioni C# tipizzate con indici stabili espliciti; filtraggio e paginazione nascondono le righe senza disabilitarne i campi. Le percentuali passano dal binder it-IT e sono convertite in frazioni decimali prima di raggiungere il dominio. L'ordinamento numerico nel browser confronta stringhe decimali canoniche, senza arrotondamenti floating point. Formato dei backup e schema persistente invariati.

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
