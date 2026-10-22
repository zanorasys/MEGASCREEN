using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Infrastructure;
using MegaScreen.Web.Models;
using MegaScreen.Web.Services;

namespace MegaScreen.Web.Pages.Painel;

public class IndexModel(AppRepository repository, SenhaService senhas) : PageModel
{
    public int FilialId { get; private set; }
    public string FilialNome { get; private set; } = "";
    public string LocalAtual { get; private set; } = "Recepção I";
    public List<Modalidade> Modalidades { get; private set; } = new();
    public List<SenhaDto> Fila { get; private set; } = new();
    public DashboardStats Stats { get; private set; } = new();
    public List<PontoAtendimento> Pontos { get; private set; } = new();
    public List<SalaEspera> SalasEspera { get; private set; } = new();
    public List<Sala> Salas { get; private set; } = new();
    public PersonalizacaoGeral Personalizacao { get; private set; } = new();

    /// <summary>Sala de espera do local atual (o guichê escolhido). Nula se o local não for um guichê cadastrado.</summary>
    public SalaEspera? SalaDoLocal { get; private set; }

    /// <summary>Guichês exibidos no card "Pontos de atendimento": os da sala de espera do local, ou todos.</summary>
    public List<PontoAtendimento> PontosDoCard =>
        SalaDoLocal is null ? Pontos : Pontos.Where(p => p.SalaEsperaId == SalaDoLocal.Id).ToList();

    public async Task OnGetAsync()
    {
        FilialId = User.FilialId();
        FilialNome = User.FindFirst("FilialNome")?.Value ?? "";
        LocalAtual = User.LocalAtual();
        Modalidades = await repository.ListarModalidadesAsync(FilialId, true);
        Fila = (await senhas.FilaAsync(FilialId)).Select(SenhaDto.From).ToList();
        Stats = await repository.ObterDashboardAsync(FilialId);
        Pontos = await repository.ListarPontosAsync(FilialId);
        SalasEspera = await repository.ListarSalasEsperaAsync(FilialId);
        Salas = await repository.ListarSalasAsync(FilialId);
        Personalizacao = await repository.ObterPersonalizacaoAsync(FilialId);
        var ponto = Pontos.FirstOrDefault(p => string.Equals(p.Nome, LocalAtual, StringComparison.OrdinalIgnoreCase));
        SalaDoLocal = ponto is null ? null : SalasEspera.FirstOrDefault(s => s.Id == ponto.SalaEsperaId);
        ViewData["FilialId"] = FilialId;
        ViewData["Painel"] = true; // o layout mostra o local atual no lugar do título
    }
}
