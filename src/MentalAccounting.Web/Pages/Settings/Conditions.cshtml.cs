using MentalAccounting.Application;
using MentalAccounting.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
namespace MentalAccounting.Web.Pages.Settings;
public sealed class ConditionsModel(AccountingService service, IDataProtectionProvider protection, TimeProvider time) : LedgerPage(service, protection, time)
{
    [BindProperty] public DateOnly Start { get; set; }
    [BindProperty] public decimal Balance { get; set; }
    [BindProperty] public decimal Gross { get; set; }
    [BindProperty] public decimal Tax { get; set; }
    [BindProperty] public DayBasis Basis { get; set; }
    [BindProperty] public int Months { get; set; } = 1;
    [BindProperty] public bool Civil { get; set; } = true;
    [BindProperty] public DateOnly? Anchor { get; set; }
    [BindProperty] public CreditTiming Timing { get; set; }
    [BindProperty] public int CreditDay { get; set; } = 1;
    [BindProperty] public List<ConditionInput> Rates { get; set; } = [];
    [BindProperty] public List<ConditionInput> Taxes { get; set; } = [];
    public async Task OnGetAsync()
    {
        await Load(); Start = Today;
        if (State.Document.Ledger is not {} l) { Rates.Add(new() { From=Today }); Taxes.Add(new() { From=Today }); return; }
        Start=l.Start; Balance=l.OpeningBalance; Gross=l.OpeningGross; Tax=l.OpeningTax; Basis=l.Basis;
        Months=l.Calendar.Months; Civil=l.Calendar.Civil; Anchor=l.Calendar.Anchor; Timing=l.Calendar.Timing; CreditDay=l.Calendar.CreditDay;
        Rates=l.Rates.Select(x=>new ConditionInput { From=x.From, Percentage=x.Rate*100 }).ToList(); Taxes=l.Taxes.Select(x=>new ConditionInput { From=x.From, Percentage=x.Rate*100 }).ToList();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        var version=Version; await Load();
        GridInputValidation.Conditions(Rates, nameof(Rates), ModelState);
        GridInputValidation.Conditions(Taxes, nameof(Taxes), ModelState);
        if (!ModelState.IsValid) return Page();
        try
        {
            var ledger = new Ledger(Start, Balance, Gross, Tax, Basis, new(Months,Civil,Anchor,Timing,CreditDay),
                State.Document.Ledger?.Movements ?? [], Rates.Select(x=>new RateChange(x.From!.Value,x.Percentage!.Value/100)).ToList(), Taxes.Select(x=>new TaxChange(x.From!.Value,x.Percentage!.Value/100)).ToList());
            return await Preview(State.Document with { Ledger=ledger }, version);
        }
        catch(Exception e) when(e is BusinessException or ConcurrencyException) { return Failure(e); }
    }
}
