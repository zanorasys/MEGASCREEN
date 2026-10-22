using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Models;
using MegaScreen.Web.Services;

namespace MegaScreen.Web.Pages.Public;

/// <summary>TV pública. /Public/Tv?telaId=3 mostra as chamadas da sala de espera da tela;
/// /Public/Tv?filialId=1 mostra a filial inteira.</summary>
[AllowAnonymous]
public class TvModel(SenhaService senhas, AppRepository repository) : PageModel
{
    public int FilialId { get; private set; }
    public int? TelaId { get; private set; }
    public bool Oculta { get; private set; }
    public string Subtitulo { get; private set; } = "";
    public List<TvChamadaDto> Ultimas { get; private set; } = new();
    public PersonalizacaoGeral Personalizacao { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int? telaId, int filialId = 1)
    {
        int? salaEsperaId = null;
        string? salaNome = null;
        if (telaId is > 0)
        {
            var tela = await repository.ObterTelaAsync(telaId.Value);
            if (tela is null) return NotFound();
            TelaId = tela.Id;
            filialId = tela.FilialId;
            salaEsperaId = tela.SalaEsperaId;
            salaNome = tela.SalaEspera?.Nome;
            Oculta = !tela.Visivel;
        }

        FilialId = filialId;
        var filial = await repository.ObterFilialAsync(filialId);
        if (filial is null) return NotFound();
        Personalizacao = await repository.ObterPersonalizacaoAsync(filialId);
        Subtitulo = salaNome is null ? filial.Nome : $"{salaNome} · {filial.Nome}";
        if (!Oculta) Ultimas = await senhas.UltimasParaTvAsync(filialId, salaEsperaId, Personalizacao);
        return Page();
    }
}
