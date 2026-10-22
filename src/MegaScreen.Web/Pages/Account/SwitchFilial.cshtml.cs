using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;

namespace MegaScreen.Web.Pages.Account;

[Authorize(Roles="Admin")]
public sealed class SwitchFilialModel(AppRepository repository) : PageModel
{
    public async Task<IActionResult> OnPostAsync(int filialId)
    {
        var filial = await repository.ObterFilialAsync(filialId);
        if (filial is null || !filial.Ativa) return BadRequest();

        var identity = (ClaimsIdentity?)User.Identity;
        if (identity is null) return Challenge();

        Replace(identity, "FilialId", filial.Id.ToString());
        Replace(identity, "FilialNome", filial.Nome);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true, AllowRefresh = true });

        return RedirectToPage("/Painel/Index");
    }

    private static void Replace(ClaimsIdentity identity, string type, string value)
    {
        foreach (var c in identity.FindAll(type).ToList()) identity.RemoveClaim(c);
        identity.AddClaim(new Claim(type, value));
    }
}
