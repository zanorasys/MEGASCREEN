using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Infrastructure;
using MegaScreen.Web.Models;

namespace MegaScreen.Web.Pages.Salas;

public class IndexModel(AppRepository repository) : PageModel
{
    public List<Sala> Itens { get; private set; }=new();
    [BindProperty] public Sala Item { get; set; }=new();
    public async Task OnGetAsync(int? edit){var fid=User.FilialId();ViewData["FilialId"]=fid;Itens=await repository.ListarSalasAsync(fid);Item=edit is >0?Itens.FirstOrDefault(x=>x.Id==edit)??new Sala{FilialId=fid}:new Sala{FilialId=fid,Cor="#4C5BF5"};}
    public async Task<IActionResult> OnPostSaveAsync(){Item.FilialId=User.FilialId();if(!ModelState.IsValid){await OnGetAsync(Item.Id==0?null:Item.Id);return Page();}await repository.SalvarSalaAsync(Item);TempData["Toast"]="Sala salva.";return RedirectToPage();}
    public async Task<IActionResult> OnPostDeleteAsync(int id){var ok=await repository.ExcluirSalaAsync(id,User.FilialId());TempData["Toast"]=ok?"Sala removida.":"Não foi possível remover: a sala já foi usada em atendimentos.";return RedirectToPage();}
}