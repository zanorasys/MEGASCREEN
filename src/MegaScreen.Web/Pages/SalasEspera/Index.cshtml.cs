using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Infrastructure;
using MegaScreen.Web.Models;

namespace MegaScreen.Web.Pages.SalasEspera;

// Importante: esta página tem dois formulários (sala de espera e ponto).
// Com [BindProperty] nos dois objetos o ASP.NET cria e valida ambos em todo POST,
// e o "Nome" vazio do outro formulário gerava "The Nome field is required" mesmo com o campo preenchido.
// Por isso cada handler recebe somente o objeto do seu formulário, com prefixo explícito.
public class IndexModel(AppRepository repository) : PageModel
{
    public List<SalaEspera> Itens { get; private set; } = new();
    public List<PontoAtendimento> Pontos { get; private set; } = new();
    public SalaEspera Item { get; set; } = new();
    public PontoAtendimento Ponto { get; set; } = new();

    public async Task OnGetAsync(int? edit)
    {
        var fid = User.FilialId();
        ViewData["FilialId"] = fid;
        Itens = await repository.ListarSalasEsperaAsync(fid);
        Pontos = await repository.ListarPontosAsync(fid);
        Item = edit is > 0 ? Itens.FirstOrDefault(x => x.Id == edit) ?? new SalaEspera { FilialId = fid } : new SalaEspera { FilialId = fid };
    }

    public async Task<IActionResult> OnPostSaveAsync([Bind(Prefix = "Item")] SalaEspera item)
    {
        item.FilialId = User.FilialId();
        if (!ModelState.IsValid)
        {
            await OnGetAsync(null);
            Item = item;
            return Page();
        }
        await repository.SalvarSalaEsperaAsync(item);
        TempData["Toast"] = "Sala de espera salva.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        await repository.ExcluirSalaEsperaAsync(id, User.FilialId());
        TempData["Toast"] = "Sala de espera removida.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSavePontoAsync([Bind(Prefix = "Ponto")] PontoAtendimento ponto)
    {
        if (string.IsNullOrWhiteSpace(ponto.Nome) || ponto.SalaEsperaId <= 0)
        {
            TempData["Toast"] = "Informe a sala de espera e o nome do ponto.";
            return RedirectToPage();
        }
        try
        {
            await repository.SalvarPontoAsync(ponto, User.FilialId());
        }
        catch (InvalidOperationException ex)
        {
            TempData["Toast"] = ex.Message;
            return RedirectToPage();
        }
        TempData["Toast"] = "Ponto de atendimento salvo.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeletePontoAsync(int id)
    {
        await repository.ExcluirPontoAsync(id, User.FilialId());
        TempData["Toast"] = "Ponto removido.";
        return RedirectToPage();
    }
}
