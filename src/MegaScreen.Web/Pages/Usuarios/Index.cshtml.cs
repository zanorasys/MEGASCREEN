using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Infrastructure;
using MegaScreen.Web.Models;
using MegaScreen.Web.Services;

namespace MegaScreen.Web.Pages.Usuarios;

public class IndexModel(AppRepository repository) : PageModel
{
    public List<Usuario> Itens { get; private set; } = new();
    [BindProperty] public Usuario Item { get; set; } = new();
    [BindProperty] public string? NovaSenha { get; set; }

    public async Task OnGetAsync(int? edit)
    {
        var fid=User.FilialId();
        ViewData["FilialId"]=fid;
        Itens=await repository.ListarUsuariosAsync(fid);
        Item = edit is >0 ? Itens.FirstOrDefault(x=>x.Id==edit) ?? new Usuario { FilialId=fid } : new Usuario { FilialId=fid, Ativo=true };
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        var fid=User.FilialId(); Item.FilialId=fid;
        if(string.IsNullOrWhiteSpace(Item.Nome) || string.IsNullOrWhiteSpace(Item.Email))
            ModelState.AddModelError("", "Nome e e-mail são obrigatórios.");
        if(Item.Id==0 && string.IsNullOrWhiteSpace(NovaSenha))
            ModelState.AddModelError(nameof(NovaSenha), "Informe a senha inicial.");
        if(!string.IsNullOrWhiteSpace(NovaSenha) && NovaSenha.Length<8)
            ModelState.AddModelError(nameof(NovaSenha), "A senha deve ter ao menos 8 caracteres.");
        if(!ModelState.IsValid) { await OnGetAsync(Item.Id==0?null:Item.Id); return Page(); }
        await repository.SalvarUsuarioAsync(Item, string.IsNullOrWhiteSpace(NovaSenha)?null:PasswordHasher.Hash(NovaSenha));
        TempData["Toast"]="Usuário salvo.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        await repository.ExcluirUsuarioAsync(id,User.FilialId());
        TempData["Toast"]="Usuário desativado.";
        return RedirectToPage();
    }
}