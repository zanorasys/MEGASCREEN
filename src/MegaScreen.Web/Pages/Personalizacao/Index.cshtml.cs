using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Infrastructure;
using MegaScreen.Web.Models;

namespace MegaScreen.Web.Pages.Personalizacao;

[RequestSizeLimit(111L * 1024 * 1024)] // vídeo 100 MB + 2 imagens 5 MB + folga
[RequestFormLimits(MultipartBodyLengthLimit = 111L * 1024 * 1024)]
public partial class IndexModel(AppRepository repository, IWebHostEnvironment env) : PageModel
{
    [BindProperty] public PersonalizacaoGeral Item { get; set; } = new();
    [BindProperty] public IFormFile? Logo { get; set; }
    [BindProperty] public IFormFile? Fundo { get; set; }
    [BindProperty] public IFormFile? Video { get; set; }
    [BindProperty] public bool RemoverLogo { get; set; }
    [BindProperty] public bool RemoverFundo { get; set; }
    [BindProperty] public bool RemoverVideo { get; set; }

    private const long LimiteImagem = 5 * 1024 * 1024;   // 5 MB
    private const long LimiteVideo = 100 * 1024 * 1024;  // 100 MB (no IIS ajuste também maxAllowedContentLength)

    // SVG não é aceito: pode conter script (XSS) e é servido no mesmo domínio do sistema.
    private static readonly string[] ExtImagem = [".png", ".jpg", ".jpeg", ".webp", ".gif"];
    private static readonly string[] ExtVideo = [".mp4", ".webm", ".ogg"];

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex CorHex();

    public async Task OnGetAsync()
    {
        var fid = User.FilialId();
        ViewData["FilialId"] = fid;
        Item = await repository.ObterPersonalizacaoAsync(fid);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var fid = User.FilialId();
        ViewData["FilialId"] = fid;
        var atual = await repository.ObterPersonalizacaoAsync(fid);
        Item.FilialId = fid;

        var erroArquivo = Validar(Logo, "logo") ?? Validar(Fundo, "fundo") ?? Validar(Video, "video");
        if (erroArquivo is not null) ModelState.AddModelError(string.Empty, erroArquivo);

        Item.MidiaUrl = string.IsNullOrWhiteSpace(Item.MidiaUrl) ? null : Item.MidiaUrl.Trim();
        if (Item.MidiaUrl is not null && !VideoLink.Valido(Item.MidiaUrl))
            ModelState.AddModelError("Item.MidiaUrl", "Link de vídeo não reconhecido. Use um link do YouTube ou do Vimeo.");

        if (!ModelState.IsValid)
        {
            Item.LogoUrl = atual.LogoUrl; Item.FundoUrl = atual.FundoUrl; Item.VideoUrl = atual.VideoUrl;
            return Page();
        }

        Item.LogoUrl = await SaveFileAsync(Logo, "logo") ?? (RemoverLogo ? null : atual.LogoUrl);
        Item.FundoUrl = await SaveFileAsync(Fundo, "fundo") ?? (RemoverFundo ? null : atual.FundoUrl);
        Item.VideoUrl = await SaveFileAsync(Video, "video") ?? (RemoverVideo ? null : atual.VideoUrl);

        Item.CorFundo = Cor(Item.CorFundo, "#0B0D24");
        Item.CorTexto = Cor(Item.CorTexto, "#FFFFFF");
        Item.CorDestaque = Cor(Item.CorDestaque, "#4C5BF5");
        Item.CorSecundaria = Cor(Item.CorSecundaria, "#7C5CFC");
        Item.Marca = string.IsNullOrWhiteSpace(Item.Marca) ? "Sua Marca" : Item.Marca.Trim();
        Item.Fonte = PersonalizacaoGeral.FontesPermitidas.Contains(Item.Fonte) ? Item.Fonte : "Manrope";
        Item.FundoPadrao = Item.FundoPadrao is not null && PersonalizacaoGeral.Fundos.ContainsKey(Item.FundoPadrao) ? Item.FundoPadrao : null;
        Item.FonteLateralPx = Math.Clamp(Item.FonteLateralPx, 10, 24);
        Item.AlertaEsperaMin = Math.Clamp(Item.AlertaEsperaMin, 0, 240);
        if (!Enum.IsDefined(Item.NomeNaTv)) Item.NomeNaTv = NomeNaTv.PrimeiroNomeInicial;
        if (!Enum.IsDefined(Item.AvisoChamada)) Item.AvisoChamada = AvisoChamada.Som;

        await repository.SalvarPersonalizacaoAsync(Item);
        TempData["Toast"] = "Personalização salva. As telas atualizam ao recarregar.";
        return RedirectToPage();
    }

    private static string Cor(string? v, string padrao) => !string.IsNullOrWhiteSpace(v) && CorHex().IsMatch(v) ? v.ToUpperInvariant() : padrao;

    private static string? Validar(IFormFile? file, string prefix)
    {
        if (file is null || file.Length == 0) return null;
        var video = prefix == "video";
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!(video ? ExtVideo : ExtImagem).Contains(ext))
            return video ? "Vídeo em formato não permitido. Use MP4, WEBM ou OGG." : "Imagem em formato não permitido. Use PNG, JPG, WEBP ou GIF.";
        if (file.Length > (video ? LimiteVideo : LimiteImagem))
            return video ? "O vídeo deve ter no máximo 100 MB." : "A imagem deve ter no máximo 5 MB.";
        return null;
    }

    private async Task<string?> SaveFileAsync(IFormFile? file, string prefix)
    {
        if (file is null || file.Length == 0) return null;
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var dir = Path.Combine(env.WebRootPath, "uploads");
        Directory.CreateDirectory(dir);
        var name = $"{prefix}-{Guid.NewGuid():N}{ext}";
        await using var fs = System.IO.File.Create(Path.Combine(dir, name));
        await file.CopyToAsync(fs);
        return "/uploads/" + name;
    }
}
