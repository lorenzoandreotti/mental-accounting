using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Security.Cryptography;
using MentalAccounting.Application;
using MentalAccounting.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
namespace MentalAccounting.Web.Pages;
public sealed class IndexModel(AccountingService service, IDataProtectionProvider protection, TimeProvider time) : LedgerPage(service, protection, time)
{
    [BindProperty] public Guid? Id { get; set; }
    [BindProperty] public DateOnly Date { get; set; }
    [BindProperty] public MovementKind Kind { get; set; }
    [BindProperty, Range(typeof(decimal), "0.01", "1000000000000", ParseLimitsInInvariantCulture = true, ErrorMessage="Inserire un importo positivo, massimo 1000 miliardi di euro.")] public decimal Amount { get; set; }
    [BindProperty, StringLength(500,ErrorMessage="La nota può contenere al massimo 500 caratteri.")] public string? Note { get; set; }
    public CalculationResult? Result { get; private set; }
    public IReadOnlyList<AccountActivity> Activities { get; private set; } = [];
    public bool FormOpen { get; private set; }
    public bool DeleteOpen { get; private set; }
    public bool Conflict { get; private set; }
    public Movement? Deleting { get; private set; }
    [BindProperty] public string DeleteTicket { get; set; } = "";
    private IDataProtector DeleteProtector => Protector.CreateProtector("DeleteMovement.v1");
    public DateOnly AsOfDate => Today;
    private async Task LoadScreen()
    {
        await Load();
        if(State.Document.Ledger is {} ledger)
        { Result=Service.Calculate(ledger,Today,includeHistory:true); Activities=AccountActivity.Build(ledger,Result); }
    }
    public async Task OnGetAsync(Guid? edit, bool add = false)
    {
        await LoadScreen(); Date = Today; FormOpen = add || edit.HasValue;
        if (edit is {} id)
        {
            var movement = State.Document.Ledger?.Movements.SingleOrDefault(x => x.Id == id);
            if (movement is null) { ModelState.AddModelError("", "Movimento non trovato o non modificabile."); return; }
            Id = movement.Id; Date = movement.Date; Kind = movement.Kind; Amount = movement.Amount; Note = movement.Note;
        }
    }
    public async Task<IActionResult> OnPostAsync()
    {
        var version = Version; await LoadScreen(); Version=version; FormOpen=true;
        if (!ModelState.IsValid) return Page();
        try
        {
            var ledger = RequireLedger();
            if (Id is not null && ledger.Movements.All(x=>x.Id!=Id)) throw new BusinessException("Movimento non trovato.");
            var movements = ledger.Movements.Where(x=>x.Id!=Id).ToList();
            movements.Add(new(Id ?? Guid.NewGuid(), Date, Kind, Amount, Note));
            await Service.Commit(State.Document with { Ledger = ledger with { Movements = movements } }, version, Today, HttpContext.RequestAborted);
            TempData["Message"]="Movimento salvato. Saldo e interessi sono stati ricalcolati.";
            return RedirectToPage("/Index");
        }
        catch(Exception e) when(e is BusinessException or ConcurrencyException) { Conflict=e is ConcurrencyException; return Failure(e); }
    }
    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var version = Version; await LoadScreen(); DeleteOpen=true;
        ModelState.Clear();
        try
        {
            var ledger=RequireLedger();
            Deleting=ledger.Movements.SingleOrDefault(x=>x.Id==id) ?? throw new BusinessException("Movimento non trovato o non modificabile.");
            var document=State.Document with { Ledger=ledger with { Movements=ledger.Movements.Where(x=>x.Id!=id).ToList() } };
            var preview=await Service.Preview(document,version,Today,HttpContext.RequestAborted);
            var ticket=DeleteProtector.Protect(JsonSerializer.Serialize(new ReviewTicket(version,document,Clock.GetUtcNow().AddMinutes(30)),BackupFormat.Options));
            Review=new(preview,ticket);
            return Page();
        }
        catch(Exception e) when(e is BusinessException or ConcurrencyException) { return Failure(e); }
    }
    public async Task<IActionResult> OnPostDeleteConfirmAsync()
    {
        ModelState.Clear();
        try
        {
            if(string.IsNullOrWhiteSpace(DeleteTicket)) throw new BusinessException("Anteprima mancante. Ripetere l'eliminazione.");
            var ticket=JsonSerializer.Deserialize<ReviewTicket>(DeleteProtector.Unprotect(DeleteTicket),BackupFormat.Options) ?? throw new BusinessException("Anteprima non valida.");
            if(ticket.Expires<=Clock.GetUtcNow()) throw new BusinessException("Anteprima scaduta. Ripetere l'eliminazione.");
            await Service.Commit(ticket.Document,ticket.Version,Today,HttpContext.RequestAborted);
            TempData["Message"]="Movimento eliminato. Saldo e interessi sono stati ricalcolati.";
            return RedirectToPage("/Index");
        }
        catch(Exception e) when(e is BusinessException or ConcurrencyException or CryptographicException or JsonException)
        {
            await LoadScreen(); DeleteOpen=true;
            return Failure(e is CryptographicException or JsonException ? new BusinessException("Anteprima non valida. Ripetere l'eliminazione.") : e);
        }
    }
}
