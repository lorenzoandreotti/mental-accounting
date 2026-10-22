using MentalAccounting.Application;
using MentalAccounting.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
namespace MentalAccounting.Web.Pages;
public sealed class SettingsModel(AccountingService service, IDataProtectionProvider protection, TimeProvider time) : LedgerPage(service, protection, time)
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
    [BindProperty] public string Rates { get; set; } = "";
    [BindProperty] public string Taxes { get; set; } = "";
    public async Task OnGetAsync()
    {
        await Load(); Start = Today;
        if (State.Document.Ledger is not {} l) return;
        Start=l.Start; Balance=l.OpeningBalance; Gross=l.OpeningGross; Tax=l.OpeningTax; Basis=l.Basis;
        Months=l.Calendar.Months; Civil=l.Calendar.Civil; Anchor=l.Calendar.Anchor; Timing=l.Calendar.Timing; CreditDay=l.Calendar.CreditDay;
        Rates=EntryParser.Format(l.Rates); Taxes=EntryParser.Format(l.Taxes);
    }
    public async Task<IActionResult> OnPostAsync()
    {
        var version=Version; await Load();
        if (!ModelState.IsValid) return Page();
        try
        {
            var ledger = new Ledger(Start, Balance, Gross, Tax, Basis, new(Months,Civil,Anchor,Timing,CreditDay),
                State.Document.Ledger?.Movements ?? [], EntryParser.Rates(Rates), EntryParser.Taxes(Taxes));
            return await Preview(State.Document with { Ledger=ledger }, version);
        }
        catch(Exception e) when(e is BusinessException or ConcurrencyException) { return Failure(e); }
    }
}
