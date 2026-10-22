using MentalAccounting.Application;
using MentalAccounting.Domain;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace MentalAccounting.Web.Pages;
public sealed class IndexModel(AccountingService service, TimeProvider time) : PageModel
{
    public AccountSnapshot State { get; set; } = new(0, new(1, null, []));
    public CalculationResult? Result { get; set; }
    public DateOnly Today => BusinessDate.Today(time);
    public async Task OnGetAsync()
    {
        State = await service.Load(HttpContext.RequestAborted);
        if (State.Document.Ledger is {} ledger) Result = service.Calculate(ledger, Today);
    }
}
