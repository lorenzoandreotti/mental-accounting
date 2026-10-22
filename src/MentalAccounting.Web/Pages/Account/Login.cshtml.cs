using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
namespace MentalAccounting.Web.Pages.Account;
[EnableRateLimiting("login")]
public sealed class LoginModel(SignInManager<IdentityUser> signIn) : PageModel
{
    [BindProperty, Required(ErrorMessage="Inserire il nome utente.")] public string Username { get; set; } = "";
    [BindProperty, Required(ErrorMessage="Inserire la password."), DataType(DataType.Password)] public string Password { get; set; } = "";
    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        if (!ModelState.IsValid) return Page();
        var result = await signIn.PasswordSignInAsync(Username, Password, false, true);
        if (result.Succeeded) return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
        ModelState.AddModelError("", "Accesso non riuscito. Verifica le credenziali o riprova più tardi.");
        return Page();
    }
}
