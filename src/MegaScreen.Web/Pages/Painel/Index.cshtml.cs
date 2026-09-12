using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Models;
using MegaScreen.Web.Services;

namespace MegaScreen.Web.Pages.Painel;

public class IndexModel(AppRepository repository, SenhaService senhas) : PageModel
{
    public int FilialId { get; private set; }
    public string LocalAtual { get; private set; } = "Recepção I";
    public List<Modalidade> Modalidades { get; private set; } = new();
    public List<SenhaDto> Fila { get; private set; } = new();

    public async Task OnGetAsync()
    {
        FilialId = int.TryParse(User.FindFirstValue("FilialId"), out var f) ? f : 1;
        LocalAtual = User.FindFirstValue("LocalAtual") ?? "Recepção I";
        Modalidades = await repository.ListarModalidadesAsync(FilialId, somenteAtivas: true);
        Fila = (await senhas.FilaAsync(FilialId)).Select(SenhaDto.From).ToList();
        ViewData["FilialId"] = FilialId;
    }
}
