using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Models;
using MegaScreen.Web.Services;

namespace MegaScreen.Web.Pages.Public;

[AllowAnonymous]
public class TvModel : PageModel
{
    private readonly SenhaService _senhas;
    public TvModel(SenhaService senhas) => _senhas = senhas;

    public int FilialId { get; private set; }
    public List<SenhaDto> Fila { get; private set; } = new();

    public async Task OnGetAsync(int filialId = 1)
    {
        FilialId = filialId;
        Fila = (await _senhas.FilaAsync(filialId)).Select(SenhaDto.From).ToList();
    }
}
