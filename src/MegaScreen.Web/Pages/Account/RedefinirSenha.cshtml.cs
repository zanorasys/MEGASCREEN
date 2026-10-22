using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Services;

namespace MegaScreen.Web.Pages.Account;

public sealed class RedefinirSenhaModel(AppRepository repository) : PageModel
{
    [BindProperty(SupportsGet=true)] public string Token { get; set; } = "";
    [BindProperty] public string NovaSenha { get; set; } = "";
    [BindProperty] public string Confirmacao { get; set; } = "";
    public bool Valido { get; private set; }

    public async Task OnGetAsync() => Valido = await ValidarAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        Valido = await ValidarAsync();
        if (!Valido) return Page();
        if (NovaSenha.Length < 8) ModelState.AddModelError(nameof(NovaSenha),"Use ao menos 8 caracteres.");
        if (NovaSenha != Confirmacao) ModelState.AddModelError(nameof(Confirmacao),"As senhas não conferem.");
        if (!ModelState.IsValid) return Page();

        var hash = Sha256(Token);
        var user = await repository.ObterUsuarioPorTokenRecuperacaoAsync(hash);
        if (user is null) { Valido=false; return Page(); }
        await repository.ConsumirTokenRecuperacaoAsync(hash,user.Id,PasswordHasher.Hash(NovaSenha));
        TempData["ResetOk"]="Senha redefinida. Faça login.";
        return RedirectToPage("/Account/Login");
    }

    private async Task<bool> ValidarAsync()
        => !string.IsNullOrWhiteSpace(Token) && await repository.ObterUsuarioPorTokenRecuperacaoAsync(Sha256(Token)) is not null;
    private static string Sha256(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
