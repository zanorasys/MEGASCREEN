using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Infrastructure;
using MegaScreen.Web.Models;

namespace MegaScreen.Web.Pages.Telas;

public class IndexModel(AppRepository repository) : PageModel
{
    public List<Tela> Itens { get; private set; }=new();
    public List<SalaEspera> SalasEspera { get; private set; }=new();
    [BindProperty] public Tela Item { get; set; }=new();
    public async Task OnGetAsync(int? edit){var fid=User.FilialId();ViewData["FilialId"]=fid;Itens=await repository.ListarTelasAsync(fid);SalasEspera=await repository.ListarSalasEsperaAsync(fid);Item=edit is >0?Itens.FirstOrDefault(x=>x.Id==edit)??new Tela{FilialId=fid}:new Tela{FilialId=fid,Cor="#4C5BF5",Visivel=true};}
    public async Task<IActionResult> OnPostSaveAsync(){Item.FilialId=User.FilialId();if(string.IsNullOrWhiteSpace(Item.Nome))ModelState.AddModelError("","Informe o nome.");if(!ModelState.IsValid){await OnGetAsync(Item.Id==0?null:Item.Id);return Page();}await repository.SalvarTelaAsync(Item);TempData["Toast"]="Tela salva.";return RedirectToPage();}
    public async Task<IActionResult> OnPostDeleteAsync(int id){await repository.ExcluirTelaAsync(id,User.FilialId());TempData["Toast"]="Tela removida.";return RedirectToPage();}
}