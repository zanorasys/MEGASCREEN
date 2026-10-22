using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Models;
using MegaScreen.Web.Services;

namespace MegaScreen.Web.Pages.Public;

[AllowAnonymous]
public class TvModel(SenhaService senhas, AppRepository repository) : PageModel
{
    public int FilialId { get; private set; }
    public List<SenhaDto> Fila { get; private set; }=new();
    public PersonalizacaoGeral Personalizacao { get; private set; }=new();
    public Filial? Filial { get; private set; }
    public async Task OnGetAsync(int filialId=1)
    {
        FilialId=filialId; Fila=(await senhas.FilaAsync(filialId)).Select(SenhaDto.From).ToList();
        Personalizacao=await repository.ObterPersonalizacaoAsync(filialId);
        Filial=await repository.ObterFilialAsync(filialId);
    }
}