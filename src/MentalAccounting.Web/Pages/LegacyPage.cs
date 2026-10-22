using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MentalAccounting.Web.Pages;

public abstract class LegacyPage(string destination) : PageModel
{
    private string Destination => Url.Page(destination) + Request.QueryString;
    public IActionResult OnGet() => LocalRedirect(Destination);
    public IActionResult OnPost() => LocalRedirectPreserveMethod(Destination);
    public IActionResult OnGetExport() => OnGet();
    public IActionResult OnPostConfirm() => OnPost();
    public IActionResult OnPostDelete() => OnPost();
    public IActionResult OnPostWorkbook() => OnPost();
    public IActionResult OnPostRestore() => OnPost();
}
