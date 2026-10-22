using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Infrastructure;
using MegaScreen.Web.Services;

namespace MegaScreen.Web.Pages.Account;

public sealed class PerfilModel(AppRepository repository) : PageModel
{
    [BindProperty] public string Nome { get; set; } = "";
    [BindProperty] public string? LocalAtual { get; set; }
    [BindProperty] public string? SenhaAtual { get; set; }
    [BindProperty] public string? NovaSenha { get; set; }
    [BindProperty] public string? Confirmacao { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await repository.ObterUsuarioAsync(User.UsuarioId(), User.FilialId());
        if (user is null) return NotFound();
        Nome = user.Nome;
        LocalAtual = user.LocalAtual;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await repository.ObterUsuarioAsync(User.UsuarioId(), User.FilialId());
        if (user is null) return NotFound();

        if (string.IsNullOrWhiteSpace(Nome)) ModelState.AddModelError(nameof(Nome), "Informe seu nome.");
        if (!string.IsNullOrWhiteSpace(NovaSenha))
        {
            if (string.IsNullOrWhiteSpace(SenhaAtual) || !PasswordHasher.Verify(SenhaAtual, user.SenhaHash))
                ModelState.AddModelError(nameof(SenhaAtual), "Senha atual inválida.");
            if (NovaSenha.Length < 8) ModelState.AddModelError(nameof(NovaSenha), "Use ao menos 8 caracteres.");
            if (NovaSenha != Confirmacao) ModelState.AddModelError(nameof(Confirmacao), "As senhas não conferem.");
        }
        if (!ModelState.IsValid) return Page();

        Nome = Nome.Trim();
        LocalAtual = string.IsNullOrWhiteSpace(LocalAtual) ? null : LocalAtual.Trim();
        await repository.AtualizarPerfilAsync(user.Id, user.FilialId, Nome, LocalAtual);
        if (!string.IsNullOrWhiteSpace(NovaSenha))
            await repository.AlterarSenhaAsync(user.Id, user.FilialId, PasswordHasher.Hash(NovaSenha));

        var identity = (ClaimsIdentity)User.Identity!;
        Replace(identity, ClaimTypes.Name, Nome);
        Replace(identity, "LocalAtual", LocalAtual ?? "");
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, AllowRefresh = true });

        TempData["Toast"] = "Perfil atualizado.";
        return RedirectToPage("/Painel/Index");
    }

    private static void Replace(ClaimsIdentity identity, string type, string value)
    {
        foreach (var c in identity.FindAll(type).ToList()) identity.RemoveClaim(c);
        identity.AddClaim(new Claim(type, value));
    }
}
