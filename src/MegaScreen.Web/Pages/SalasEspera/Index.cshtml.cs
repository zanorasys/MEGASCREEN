using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Infrastructure;
using MegaScreen.Web.Models;

namespace MegaScreen.Web.Pages.SalasEspera;

public class IndexModel(AppRepository repository) : PageModel
{
    public List<SalaEspera> Itens { get; private set; }=new();
    public List<PontoAtendimento> Pontos { get; private set; }=new();
    [BindProperty] public SalaEspera Item { get; set; }=new();
    [BindProperty] public PontoAtendimento Ponto { get; set; }=new();
    public async Task OnGetAsync(int? edit){var fid=User.FilialId();ViewData["FilialId"]=fid;Itens=await repository.ListarSalasEsperaAsync(fid);Pontos=await repository.ListarPontosAsync(fid);Item=edit is >0?Itens.FirstOrDefault(x=>x.Id==edit)??new SalaEspera{FilialId=fid}:new SalaEspera{FilialId=fid};}
    public async Task<IActionResult> OnPostSaveAsync(){Item.FilialId=User.FilialId();if(string.IsNullOrWhiteSpace(Item.Nome))ModelState.AddModelError("","Informe o nome.");if(!ModelState.IsValid){await OnGetAsync(Item.Id==0?null:Item.Id);return Page();}await repository.SalvarSalaEsperaAsync(Item);TempData["Toast"]="Sala de espera salva.";return RedirectToPage();}
    public async Task<IActionResult> OnPostDeleteAsync(int id){await repository.ExcluirSalaEsperaAsync(id,User.FilialId());TempData["Toast"]="Sala de espera removida.";return RedirectToPage();}
    public async Task<IActionResult> OnPostSavePontoAsync(){if(string.IsNullOrWhiteSpace(Ponto.Nome)||Ponto.SalaEsperaId<=0){TempData["Toast"]="Informe sala e nome do ponto.";return RedirectToPage();}await repository.SalvarPontoAsync(Ponto,User.FilialId());TempData["Toast"]="Ponto de atendimento salvo.";return RedirectToPage();}
    public async Task<IActionResult> OnPostDeletePontoAsync(int id){await repository.ExcluirPontoAsync(id,User.FilialId());TempData["Toast"]="Ponto removido.";return RedirectToPage();}
}