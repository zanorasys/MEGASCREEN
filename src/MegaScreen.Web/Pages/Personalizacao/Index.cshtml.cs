using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Infrastructure;
using MegaScreen.Web.Models;

namespace MegaScreen.Web.Pages.Personalizacao;

public class IndexModel(AppRepository repository, IWebHostEnvironment env) : PageModel
{
    [BindProperty] public PersonalizacaoGeral Item { get; set; }=new();
    [BindProperty] public IFormFile? Logo { get; set; }
    [BindProperty] public IFormFile? Fundo { get; set; }
    [BindProperty] public IFormFile? Video { get; set; }

    public async Task OnGetAsync(){var fid=User.FilialId();ViewData["FilialId"]=fid;Item=await repository.ObterPersonalizacaoAsync(fid);}

    public async Task<IActionResult> OnPostAsync()
    {
        var fid=User.FilialId();
        var atual=await repository.ObterPersonalizacaoAsync(fid);
        Item.FilialId=fid;
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

    private async Task<string?> SaveFileAsync(IFormFile? file,string prefix)
    {
        if(file is null||file.Length==0)return null;
        var ext=Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed=prefix=="video"?new[]{".mp4",".webm",".ogg"}:new[]{".png",".jpg",".jpeg",".webp",".gif",".svg"};
        if(!allowed.Contains(ext))throw new InvalidOperationException("Formato de arquivo não permitido.");
        var dir=Path.Combine(env.WebRootPath,"uploads");Directory.CreateDirectory(dir);
        var name=$"{prefix}-{Guid.NewGuid():N}{ext}";
        await using var fs=System.IO.File.Create(Path.Combine(dir,name));await file.CopyToAsync(fs);
        return "/uploads/"+name;
    }
    private static string NormalizaCor(string? v,string fallback)=>!string.IsNullOrWhiteSpace(v)&&v.StartsWith("#")?v:fallback;
}