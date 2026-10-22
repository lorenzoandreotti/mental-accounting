using System.ComponentModel.DataAnnotations;
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
        var version = Version; await LoadScreen(); FormOpen=true;
        if (!ModelState.IsValid) return Page();
        try
        {
            var ledger = RequireLedger();
            if (Id is not null && ledger.Movements.All(x=>x.Id!=Id)) throw new BusinessException("Movimento non trovato.");
            var movements = ledger.Movements.Where(x=>x.Id!=Id).ToList();
            movements.Add(new(Id ?? Guid.NewGuid(), Date, Kind, Amount, Note));
            return await Preview(State.Document with { Ledger = ledger with { Movements = movements } }, version);
        }
        catch(Exception e) when(e is BusinessException or ConcurrencyException) { return Failure(e); }
    }
    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var version = Version; await LoadScreen(); FormOpen=true;
        ModelState.Remove(nameof(Amount));
        try { var ledger = RequireLedger(); if(!ledger.Movements.Any(x=>x.Id==id)) throw new BusinessException("Movimento non trovato o non modificabile."); return await Preview(State.Document with { Ledger = ledger with { Movements = ledger.Movements.Where(x=>x.Id!=id).ToList() } }, version); }
        catch(Exception e) when(e is BusinessException or ConcurrencyException) { return Failure(e); }
    }
}
