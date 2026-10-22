using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Models;

namespace MegaScreen.Web.Pages.Clientes;

[Authorize(Roles="Admin")]
public class IndexModel(AppRepository repository) : PageModel
{
    public List<Cliente> Itens { get; private set; }=new();
    [BindProperty] public Cliente Item { get; set; }=new();
    public async Task OnGetAsync(int? edit){Itens=await repository.ListarClientesAsync();Item=edit is >0?Itens.FirstOrDefault(x=>x.Id==edit)??new Cliente():new Cliente{Ativo=true};}
    public async Task<IActionResult> OnPostSaveAsync(){var digitos=new string((Item.Documento??"").Where(char.IsDigit).ToArray());if(digitos.Length>0&&digitos.Length!=(Item.Tipo==TipoCliente.PJ?14:11))ModelState.AddModelError("Item.Documento",Item.Tipo==TipoCliente.PJ?"CNPJ deve ter 14 dígitos.":"CPF deve ter 11 dígitos.");if(!ModelState.IsValid){await OnGetAsync(Item.Id==0?null:Item.Id);return Page();}await repository.SalvarClienteAsync(Item);TempData["Toast"]="Cliente salvo.";return RedirectToPage();}
    public async Task<IActionResult> OnPostDeleteAsync(int id){await repository.ExcluirClienteAsync(id);TempData["Toast"]="Cliente desativado.";return RedirectToPage();}
}