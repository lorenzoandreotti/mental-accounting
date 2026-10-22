using MentalAccounting.Application;
using MentalAccounting.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace MentalAccounting.Web.Pages;
public sealed class InterestModel(AccountingService service, TimeProvider time) : PageModel
{
    public CalculationResult? Result { get; set; }
    [BindProperty(SupportsGet=true)] public DateOnly? Through { get; set; }
    public async Task OnGetAsync()
    {
        var state=await service.Load();
        var today=BusinessDate.Today(time); Through ??=today;
        if(state.Document.Ledger is not {} ledger) return;
        try
        {
            if(Through>today) throw new BusinessException("Usa le simulazioni per le date future.");
            Result=service.Calculate(ledger, Through.Value);
        }
        catch(BusinessException e){ModelState.AddModelError("",e.Message);}
    }
}
