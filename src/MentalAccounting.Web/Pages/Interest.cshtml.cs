using MentalAccounting.Application;
using MentalAccounting.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace MentalAccounting.Web.Pages;
public sealed class InterestModel(AccountingService service, TimeProvider time) : PageModel
{
    public CalculationResult? Result { get; set; }
    [BindProperty(SupportsGet=true)] public DateOnly? Through { get; set; }
    [BindProperty(SupportsGet=true)] public DateOnly? PeriodStart { get; set; }
    public async Task OnGetAsync()
    {
        var state=await service.Load();
        var today=BusinessDate.Today(time); Through ??=today;
        if(state.Document.Ledger is not {} ledger) return;
        try
        {
            if(Through>today) throw new BusinessException("Usa le simulazioni per le date future.");
            if (PeriodStart is {} start)
            {
                var calculation = service.Calculate(ledger, today);
                var period = calculation.Settlements.SingleOrDefault(x => x.From == start && x.Through == Through.Value)
                    ?? throw new BusinessException("Periodo di calcolo non trovato. Selezionare un accredito dalla cronologia.");
                Result = calculation with
                {
                    Settlements = [period],
                    Segments = calculation.Segments.Where(x => x.From >= period.From && x.Through <= period.Through).ToArray()
                };
                ViewData["SelectedPeriod"] = period;
            }
            else Result=service.Calculate(ledger, Through.Value);
        }
        catch(BusinessException e){ModelState.AddModelError("",e.Message);}
    }
}
