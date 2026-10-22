using System.ComponentModel.DataAnnotations;
using MentalAccounting.Application;
using MentalAccounting.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
namespace MentalAccounting.Web.Pages;
public sealed class MovementsModel(AccountingService service, IDataProtectionProvider protection, TimeProvider time) : LedgerPage(service, protection, time)
{
    [BindProperty] public Guid? Id { get; set; }
    [BindProperty] public DateOnly Date { get; set; }
    [BindProperty] public MovementKind Kind { get; set; }
    [BindProperty, Range(typeof(decimal), "0.01", "1000000000000", ParseLimitsInInvariantCulture = true, ErrorMessage="Inserire un importo positivo, massimo 1000 miliardi di euro.")] public decimal Amount { get; set; }
    [BindProperty, StringLength(500,ErrorMessage="La nota può contenere al massimo 500 caratteri.")] public string? Note { get; set; }
    public async Task OnGetAsync(Guid? edit)
    {
        await Load(); Date = Today;
        if (edit is {} id && State.Document.Ledger?.Movements.SingleOrDefault(x=>x.Id==id) is {} m)
        { Id = m.Id; Date = m.Date; Kind = m.Kind; Amount = m.Amount; Note = m.Note; }
    }
    public async Task<IActionResult> OnPostAsync()
    {
        var version = Version; await Load();
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
        var version = Version; await Load();
        try { var ledger = RequireLedger(); return await Preview(State.Document with { Ledger = ledger with { Movements = ledger.Movements.Where(x=>x.Id!=id).ToList() } }, version); }
        catch(Exception e) when(e is BusinessException or ConcurrencyException) { return Failure(e); }
    }
}
