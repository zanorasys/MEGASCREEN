using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Infrastructure;
using MegaScreen.Web.Services;

namespace MegaScreen.Web.Pages.Account;

public sealed class EsqueciSenhaModel(AppRepository repository, IEmailSender emailSender, IWebHostEnvironment env) : PageModel
{
    [BindProperty] public string Email { get; set; } = "";
    public string? DevLink { get; private set; }
    public bool Enviado { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        Enviado = true;
        var user = await repository.ObterUsuarioPorEmailAsync(Email.Trim());
        if (user is null) return Page();

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        await repository.CriarTokenRecuperacaoAsync(user.Id, hash, DataHora.Agora().AddHours(1));

        var link = Url.Page("/Account/RedefinirSenha", null, new { token }, Request.Scheme)!;
        if (emailSender.Enabled)
            await emailSender.SendAsync(user.Email, "Redefinição de senha — Mega Screen",
                $"<p>Olá, {System.Net.WebUtility.HtmlEncode(user.Nome)}.</p><p><a href=\"{link}\">Clique aqui para redefinir sua senha</a>. O link expira em 1 hora.</p>");
        else if (env.IsDevelopment())
            DevLink = link; // só em desenvolvimento; em produção sem SMTP o link nunca aparece na tela
        return Page();
    }
}