using System.Text.Json;
using MentalAccounting.Application;
using MentalAccounting.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace MentalAccounting.Web.Pages;
public sealed record ReviewTicket(long Version, AccountDocument Document, DateTimeOffset Expires);
public sealed record ReviewView(ChangePreview Change, string Ticket);
public abstract class LedgerPage(AccountingService service, IDataProtectionProvider protection, TimeProvider time) : PageModel
{
    protected AccountingService Service => service;
    protected TimeProvider Clock => time;
    protected DateOnly Today => BusinessDate.Today(time);
    protected readonly IDataProtector Protector = protection.CreateProtector("Accounting.ChangeReview.v1");
    public AccountSnapshot State { get; set; } = new(0, new(1, null, []));
    public ReviewView? Review { get; set; }
    [BindProperty] public string Ticket { get; set; } = "";
    [BindProperty] public long Version { get; set; }
    protected async Task Load() { State = await service.Load(HttpContext.RequestAborted); Version = State.Version; }
    protected Ledger RequireLedger() => State.Document.Ledger ?? throw new BusinessException("Configurare prima il saldo iniziale.");
    protected async Task<IActionResult> Preview(AccountDocument document, long version)
    {
        var preview = await service.Preview(document, version, Today, HttpContext.RequestAborted);
        var ticket = Protector.Protect(JsonSerializer.Serialize(new ReviewTicket(version, document, time.GetUtcNow().AddMinutes(30)), BackupFormat.Options));
        Review = new(preview, ticket); return Page();
    }
    protected IActionResult Failure(Exception e) { ModelState.AddModelError("", e.Message); return Page(); }
    public async Task<IActionResult> OnPostConfirmAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Ticket)) throw new BusinessException("Anteprima mancante. Ripetere l'operazione.");
            var ticket = JsonSerializer.Deserialize<ReviewTicket>(Protector.Unprotect(Ticket), BackupFormat.Options) ?? throw new BusinessException("Anteprima mancante.");
            if (ticket.Expires < time.GetUtcNow()) throw new BusinessException("Anteprima scaduta. Ripetere l'operazione.");
            await service.Commit(ticket.Document, ticket.Version, Today, HttpContext.RequestAborted);
            TempData["Message"] = "Modifica salvata. Saldo e interessi sono stati ricalcolati.";
        }
        catch (Exception e) when (e is BusinessException or ConcurrencyException) { TempData["Error"] = e.Message; }
        catch (System.Security.Cryptography.CryptographicException) { TempData["Error"] = "Anteprima non valida. Ripetere l'operazione."; }
        return RedirectToPage("/Index");
    }
}
