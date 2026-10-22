# Accantonamento su conto deposito

## Scopo
Webapp personale, in italiano, per un solo accantonamento virtuale in euro all'interno di un conto deposito. Nessuna integrazione bancaria, gestione del resto del conto o modalità offline.

## Saldo e movimenti
Versamenti, prelievi e addebiti manuali per costi/bollo hanno data, importo positivo e nota facoltativa. Modificano il saldo remunerato dalla data indicata. Gli interessi non accreditati sono separati e non prelevabili. Non è ammesso saldo negativo, neppure nello storico modificato. Inserimento e modifica salvano direttamente dal modal con validazione server e ricalcolo dei periodi successivi. La cancellazione richiede anteprima e conferma in un modal dedicato, con saldo disponibile e interessi netti da accreditare prima/dopo e relative differenze. Operazioni simultanee da dispositivi diversi non devono sovrascriversi.

## Interessi
Lo storico usa interesse semplice: capitale × tasso annuo lordo × giorni / base annua. Basi configurabili: ACT/360, ACT/365 fisso e ACT/anno effettivo (365 o 366). Tassi e aliquote sono percentuali con decorrenza; l'aliquota si applica nei giorni di maturazione, la trattenuta avviene alla chiusura. Occorre una condizione valida alla data iniziale; nessun valore implicito di tasso o imposta.

Ogni giorno applica prima gli accrediti già dovuti, poi i movimenti di quel giorno, poi matura gli interessi. La data di valutazione comprende l'intera giornata. Movimenti nello stesso giorno vengono aggregati prima di verificare il saldo; il saldo di fine giornata non può essere negativo. Il dettaglio raggruppa giorni consecutivi con medesimi capitale, tasso, imposta e base.

Calendario mensile, trimestrale, semestrale o annuale: periodi civili o ancorati a una data personalizzata. Le ricorrenze si calcolano sempre dall'ancora originale, senza deriva dopo febbraio. Giorni inesistenti vengono ridotti all'ultimo giorno del mese; nessun calendario festivo. Il primo periodo può essere parziale. Le modifiche al calendario/base sono globali e ricalcolano lo storico con anteprima.

Accredito a fine periodo, primo giorno del mese successivo o giorno X del mese successivo. La chiusura avviene a fine dell'ultimo giorno del periodo. L'accredito a fine periodo entra nel saldo dopo la maturazione di quel giorno e rende dal giorno successivo; gli altri accrediti entrano a inizio della data scelta e rendono da quella data. Lordo e imposta sono arrotondati separatamente ai centesimi, con metà arrotondata lontano dallo zero; netto = lordo − imposta. Nessun arrotondamento giornaliero. Eventuali importi già chiusi in attesa di pagamento restano visibili separatamente dagli interessi aperti.

## Avvio e storico
Avvio da saldo a una data con eventuale lordo e imposta già maturati (non ancora liquidati), oppure recupero del foglio Excel. L'anteprima XLSX usa esplicitamente Sheet1, colonne C=data, D=tipo, E=importo: importa Carico, Prelievo e Addebito numerici; ignora e segnala Interessi e Consolidamento. Non esegue formule e non assume corrette le condizioni del foglio. L'importazione sostituisce i movimenti solo dopo conferma; configurazione e saldo iniziale vanno scelti coerentemente per evitare doppi conteggi. Date/importi/formule non riconosciuti producono segnalazioni visibili e impediscono la conferma finché non risolti nel file.

## Consultazione e simulazioni
Dashboard con saldo disponibile, maturato lordo, imposta e netto, incluso il maturato chiuso in attesa di accredito. Cronologia, liquidazioni e dettaglio verificabile dei calcoli. Simulazioni fino a una data futura con movimenti, tassi e aliquote ipotetici, senza modificare lo storico, salvabili come scenari. Confronto semplice/composto: solo nelle simulazioni il tasso annuo effettivo genera crescita giornaliera equivalente; la crescita lorda non pagata contribuisce alla base virtuale fino alla chiusura, mentre la capitalizzazione effettiva resta netta alla data di accredito.

Esportazione CSV dei movimenti e riepiloghi; backup JSON versionato e ripristino con anteprima. Backup applicativo senza account/password. Gli scenari vengono inclusi nel backup.

## Interfaccia
La pagina principale Movimenti riunisce riepiloghi, storico del saldo, modal di inserimento e modifica e cronologia dell'intero accantonamento. La cronologia include versamenti, prelievi, addebiti e una riga per ogni accredito netto effettivo. Gli interessi automatici sono consultabili ma non modificabili; lordo, imposta, periodo e collegamento al calcolo sono nel dettaglio della riga. I periodi chiusi in attesa restano nei riepiloghi e nel Dettaglio interessi, senza entrare nei movimenti disponibili.

Il grafico mostra due serie giornaliere a fine giornata: disponibile e totale comprensivo del netto maturato, anche già chiuso ma non pagato. Default Tutto; intervalli 1/3/6 mesi, 1 anno e personalizzato, limitati ai giorni disponibili fino a oggi. Il periodo del grafico è indipendente dai filtri della griglia. È possibile leggere i valori per data senza usare il tooltip.

Impostazioni contiene Condizioni di calcolo, Aspetto e Importa/Esporta. Tema Automatico (default, segue il sistema e nasconde il toggle nell'intestazione) o Manuale (toggle Chiaro/Scuro disponibile anche nel login). Preferenza locale al browser; passando a Manuale si mantiene il tema effettivo. Le vecchie preferenze del browser rimangono compatibili.

Tassi, aliquote e ipotesi future vengono inseriti tramite griglie con aggiunta/rimozione di righe. Tutte le griglie dati supportano ordinamento, filtri combinati per colonna e paginazione 10/25/50 righe. I filtri non cambiano i totali contabili, il grafico, i dati esportati o le righe inviate alla conferma. Le validazioni mantengono i valori e indicano le righe da correggere. Nessun salvataggio implicito dei dati contabili; le sole preferenze di aspetto si applicano subito.

## Default
Nell'importazione sono accettati anche prelievi/addebiti con segno negativo, normalizzati a importo positivo con segnalazione. Si eliminano esclusivamente residui binari Excel inferiori a 0,00000001 euro rispetto al centesimo; vere frazioni di centesimo restano errori.

Euro, cultura it-IT, base ACT/365, mese civile, accredito il primo del mese successivo. Tassi e aliquote devono essere inseriti e confermati. Orizzonte massimo 100 anni per calcoli e simulazioni. Nessun capitale negativo, tassi negativi o aliquote fuori 0–100% nella prima versione.
