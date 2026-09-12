using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Models;
using MegaScreen.Web.Services;

namespace MegaScreen.Web.Pages.Account;

public class LoginModel(AppRepository repository) : PageModel
{
    [BindProperty] public LoginViewModel Input { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = await repository.ObterUsuarioPorEmailAsync(Input.Email);
        if (user is null || !PasswordHasher.Verify(Input.Senha, user.SenhaHash))
        {
            ModelState.AddModelError(string.Empty, "E-mail ou senha inválidos.");
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Nome),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Perfil.ToString()),
            new("FilialId", user.FilialId.ToString()),
            new("LocalAtual", user.LocalAtual ?? "")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = Input.LembrarDeMim });

        return user.Perfil == PerfilUsuario.Profissional
            ? RedirectToPage("/Public/Profissional")
            : RedirectToPage("/Painel/Index");
    }
}
