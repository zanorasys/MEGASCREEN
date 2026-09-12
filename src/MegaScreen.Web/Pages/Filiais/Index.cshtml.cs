using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Models;

namespace MegaScreen.Web.Pages.Filiais;

public class IndexModel(AppRepository repository) : PageModel
{
    public List<Filial> Itens { get; private set; } = new();
    public async Task OnGetAsync() => Itens = await repository.ListarFiliaisAsync();
}
