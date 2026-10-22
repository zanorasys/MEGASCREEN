using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Infrastructure;
using MegaScreen.Web.Models;
using MegaScreen.Web.Services;

namespace MegaScreen.Web.Pages.Painel;

public class IndexModel(AppRepository repository, SenhaService senhas) : PageModel
{
    public int FilialId { get; private set; }
    public string LocalAtual { get; private set; }="Recepção I";
    public List<Modalidade> Modalidades { get; private set; }=new();
    public List<SenhaDto> Fila { get; private set; }=new();
    public DashboardStats Stats { get; private set; }=new();
    public List<PontoAtendimento> Pontos { get; private set; }=new();

    public async Task OnGetAsync()
    {
        FilialId=User.FilialId(); LocalAtual=User.LocalAtual();
        Modalidades=await repository.ListarModalidadesAsync(FilialId,true);
        Fila=(await senhas.FilaAsync(FilialId)).Select(SenhaDto.From).ToList();
        Stats=await repository.ObterDashboardAsync(FilialId);
        Pontos=await repository.ListarPontosAsync(FilialId);
        ViewData["FilialId"]=FilialId;
    }
}