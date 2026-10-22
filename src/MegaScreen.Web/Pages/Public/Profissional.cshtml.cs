using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Infrastructure;
using MegaScreen.Web.Models;
using MegaScreen.Web.Services;

namespace MegaScreen.Web.Pages.Public;

[Authorize(Roles="Profissional,Admin")]
public class ProfissionalModel(AppRepository repository,SenhaService senhas) : PageModel
{
    public int FilialId { get; private set; }
    public List<Sala> Salas { get; private set; }=new();
    public List<SenhaDto> Fila { get; private set; }=new();
    public async Task OnGetAsync(){FilialId=User.FilialId();Salas=await repository.ListarSalasAsync(FilialId);Fila=(await senhas.FilaAsync(FilialId)).Select(SenhaDto.From).ToList();}
}