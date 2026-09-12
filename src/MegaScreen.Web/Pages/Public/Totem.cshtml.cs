using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Models;

namespace MegaScreen.Web.Pages.Public;

[AllowAnonymous]
public class TotemModel(AppRepository repository) : PageModel
{
    public int FilialId { get; private set; }
    public List<Modalidade> Modalidades { get; private set; } = new();

    public async Task OnGetAsync(int filialId = 1)
    {
        FilialId = filialId;
        Modalidades = await repository.ListarModalidadesAsync(filialId, somenteAtivas: true);
    }
}
