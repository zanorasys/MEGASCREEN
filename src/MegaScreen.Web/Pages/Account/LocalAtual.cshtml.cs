using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Infrastructure;

namespace MegaScreen.Web.Pages.Account;

/// <summary>Troca rápida do "Local atual" (chip no topo do painel). Só POST.</summary>
public sealed class LocalAtualModel(AppRepository repository) : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Painel/Index");

    public async Task<IActionResult> OnPostAsync(string? local, string? localOutro)
    {
        local = (local == "__outro" ? localOutro : local)?.Trim();
        if (string.IsNullOrWhiteSpace(local) || local.Length > 80)
        {
            TempData["Toast"] = "Informe um local válido (até 80 caracteres).";
            return RedirectToPage("/Painel/Index");
        }

        await repository.AtualizarLocalAtualAsync(User.UsuarioId(), local);

        var identity = (ClaimsIdentity)User.Identity!;
        foreach (var c in identity.FindAll("LocalAtual").ToList()) identity.RemoveClaim(c);
        identity.AddClaim(new Claim("LocalAtual", local));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, AllowRefresh = true });

        TempData["Toast"] = $"Local atual: {local}";
        return RedirectToPage("/Painel/Index");
    }
}
