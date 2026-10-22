using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Infrastructure;
using MegaScreen.Web.Models;

namespace MegaScreen.Web.Pages.Modalidades;

public class IndexModel(AppRepository repository) : PageModel
{
    public List<Modalidade> Itens { get; private set; }=new();
    [BindProperty] public Modalidade Item { get; set; }=new();

    public async Task OnGetAsync(int? edit)
    {
        var fid=User.FilialId(); ViewData["FilialId"]=fid;
        Itens=await repository.ListarModalidadesAsync(fid);
        Item=edit is >0 ? Itens.FirstOrDefault(x=>x.Id==edit)??new Modalidade{FilialId=fid} : new Modalidade{FilialId=fid,Ativa=true};
    }
    public async Task<IActionResult> OnPostSaveAsync()
    {
        Item.FilialId=User.FilialId();
        if(string.IsNullOrWhiteSpace(Item.Nome)||string.IsNullOrWhiteSpace(Item.Prefixo)) ModelState.AddModelError("","Nome e prefixo são obrigatórios.");
        if(!ModelState.IsValid){await OnGetAsync(Item.Id==0?null:Item.Id);return Page();}
        await repository.SalvarModalidadeAsync(Item); TempData["Toast"]="Modalidade salva."; return RedirectToPage();
    }
    public async Task<IActionResult> OnPostDeleteAsync(int id){await repository.ExcluirModalidadeAsync(id,User.FilialId());TempData["Toast"]="Modalidade desativada.";return RedirectToPage();}
}