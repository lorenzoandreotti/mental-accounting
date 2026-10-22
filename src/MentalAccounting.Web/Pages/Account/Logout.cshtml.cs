using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace MentalAccounting.Web.Pages.Account;
public sealed class LogoutModel(SignInManager<IdentityUser> signIn) : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Index");
    public async Task<IActionResult> OnPostAsync() { await signIn.SignOutAsync(); return RedirectToPage("/Account/Login"); }
}
