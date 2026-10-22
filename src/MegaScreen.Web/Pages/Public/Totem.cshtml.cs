using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Models;

namespace MegaScreen.Web.Pages.Public;

/// <summary>Totem de autoatendimento. /Public/Totem?telaId=2 (tela tipo Tablet) ou /Public/Totem?filialId=1.</summary>
[AllowAnonymous]
public class TotemModel(AppRepository repository) : PageModel
{
    public int FilialId { get; private set; }
    public bool Oculta { get; private set; }
    public string Subtitulo { get; private set; } = "";
    public List<Modalidade> Modalidades { get; private set; } = new();
    public PersonalizacaoGeral Personalizacao { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int? telaId, int filialId = 1)
    {
        string? salaNome = null;
        if (telaId is > 0)
        {
            var tela = await repository.ObterTelaAsync(telaId.Value);
            if (tela is null) return NotFound();
            filialId = tela.FilialId;
            salaNome = tela.SalaEspera?.Nome;
            Oculta = !tela.Visivel;
        }
        FilialId = filialId;
        var filial = await repository.ObterFilialAsync(filialId);
        if (filial is null) return NotFound();
        Subtitulo = salaNome is null ? filial.Nome : $"{filial.Nome} · {salaNome}";
        Modalidades = await repository.ListarModalidadesAsync(filialId, true);
        Personalizacao = await repository.ObterPersonalizacaoAsync(filialId);
        return Page();
    }
}
