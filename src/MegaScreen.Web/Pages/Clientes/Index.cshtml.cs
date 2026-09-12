using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Models;

namespace MegaScreen.Web.Pages.Clientes;

public class IndexModel(AppRepository repository) : PageModel
{
    public List<Cliente> Itens { get; private set; } = new();
    public async Task OnGetAsync() => Itens = await repository.ListarClientesAsync();
}
