using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Models;

namespace MegaScreen.Web.Pages.Filiais;

[Authorize(Roles="Admin")]
public class IndexModel(AppRepository repository) : PageModel
{
    public List<Filial> Itens { get; private set; }=new();
    public List<Cliente> Clientes { get; private set; }=new();
    [BindProperty] public Filial Item { get; set; }=new();
    public async Task OnGetAsync(int? edit){Itens=await repository.ListarFiliaisAsync();Clientes=await repository.ListarClientesAsync();Item=edit is >0?Itens.FirstOrDefault(x=>x.Id==edit)??new Filial():new Filial{Ativa=true};}
    public async Task<IActionResult> OnPostSaveAsync(){if(string.IsNullOrWhiteSpace(Item.Nome)||Item.ClienteId<=0)ModelState.AddModelError("","Cliente e nome da filial são obrigatórios.");if(!ModelState.IsValid){await OnGetAsync(Item.Id==0?null:Item.Id);return Page();}Item.Uf=Item.Uf?.Trim().ToUpperInvariant();await repository.SalvarFilialAsync(Item);TempData["Toast"]="Filial salva.";return RedirectToPage();}
    public async Task<IActionResult> OnPostDeleteAsync(int id){await repository.ExcluirFilialAsync(id);TempData["Toast"]="Filial desativada.";return RedirectToPage();}
}