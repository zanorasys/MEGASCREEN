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
public class IndexModel(AppRepository repository, IWebHostEnvironment env) : PageModel
{
    [BindProperty] public PersonalizacaoGeral Item { get; set; }=new();
    [BindProperty] public IFormFile? Logo { get; set; }
    [BindProperty] public IFormFile? Fundo { get; set; }
    [BindProperty] public IFormFile? Video { get; set; }

    private const long LimiteImagem = 5 * 1024 * 1024;   // 5 MB
    private const long LimiteVideo = 100 * 1024 * 1024;  // 100 MB (no IIS ajuste também maxAllowedContentLength)

    public async Task OnGetAsync(){var fid=User.FilialId();ViewData["FilialId"]=fid;Item=await repository.ObterPersonalizacaoAsync(fid);}

    public async Task<IActionResult> OnPostAsync()
    {
        var fid=User.FilialId();
        var atual=await repository.ObterPersonalizacaoAsync(fid);
        Item.FilialId=fid;
        var erro=Validar(Logo,"logo")??Validar(Fundo,"fundo")??Validar(Video,"video");
        if(erro is not null)
        {
            ModelState.AddModelError(string.Empty,erro);
            ViewData["FilialId"]=fid;
            Item.LogoUrl=atual.LogoUrl;Item.FundoUrl=atual.FundoUrl;Item.VideoUrl=atual.VideoUrl;
            return Page();
        }
        Item.LogoUrl=await SaveFileAsync(Logo,"logo")??atual.LogoUrl;
        Item.FundoUrl=await SaveFileAsync(Fundo,"fundo")??atual.FundoUrl;
        Item.VideoUrl=await SaveFileAsync(Video,"video")??atual.VideoUrl;
        Item.CorFundo=NormalizaCor(Item.CorFundo,"#0B0D24");
        Item.CorTexto=NormalizaCor(Item.CorTexto,"#FFFFFF");
        Item.CorDestaque=NormalizaCor(Item.CorDestaque,"#4C5BF5");
        Item.Marca=string.IsNullOrWhiteSpace(Item.Marca)?"Sua Marca":Item.Marca.Trim();
        Item.Fonte=string.IsNullOrWhiteSpace(Item.Fonte)?"Manrope":Item.Fonte.Trim();
        await repository.SalvarPersonalizacaoAsync(Item);
        TempData["Toast"]="Personalização salva.";
        return RedirectToPage();
    }

    // SVG não é aceito: pode conter script (XSS) e é servido no mesmo domínio do sistema.
    private static readonly string[] ExtImagem={".png",".jpg",".jpeg",".webp",".gif"};
    private static readonly string[] ExtVideo={".mp4",".webm",".ogg"};

    private static string? Validar(IFormFile? file,string prefix)
    {
        if(file is null||file.Length==0)return null;
        var video=prefix=="video";
        var ext=Path.GetExtension(file.FileName).ToLowerInvariant();
        if(!(video?ExtVideo:ExtImagem).Contains(ext))
            return video?"Vídeo em formato não permitido. Use MP4, WEBM ou OGG.":"Imagem em formato não permitido. Use PNG, JPG, WEBP ou GIF.";
        if(file.Length>(video?LimiteVideo:LimiteImagem))
            return video?"O vídeo deve ter no máximo 100 MB.":"A imagem deve ter no máximo 5 MB.";
        return null;
    }

    private async Task<string?> SaveFileAsync(IFormFile? file,string prefix)
    {
        if(file is null||file.Length==0)return null;
        var ext=Path.GetExtension(file.FileName).ToLowerInvariant();
        var dir=Path.Combine(env.WebRootPath,"uploads");Directory.CreateDirectory(dir);
        var name=$"{prefix}-{Guid.NewGuid():N}{ext}";
        await using var fs=System.IO.File.Create(Path.Combine(dir,name));await file.CopyToAsync(fs);
        return "/uploads/"+name;
    }
    private static string NormalizaCor(string? v,string fallback)=>!string.IsNullOrWhiteSpace(v)&&v.StartsWith("#")?v:fallback;
}