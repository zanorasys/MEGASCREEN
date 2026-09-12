using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Models;

namespace MegaScreen.Web.Pages.Modalidades;

public class IndexModel(AppRepository repository) : PageModel
{
    protected int FilialId => int.TryParse(User.FindFirstValue("FilialId"), out var f) ? f : 1;
    public List<Modalidade> Itens { get; private set; } = new();

    public async Task OnGetAsync()
    {
        ViewData["FilialId"] = FilialId;
        Itens = await repository.ListarModalidadesAsync(FilialId);
    }
}
