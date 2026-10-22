using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MegaScreen.Web.Data;
using MegaScreen.Web.Infrastructure;
using MegaScreen.Web.Models;
using MegaScreen.Web.Services;
using Microsoft.Data.SqlClient;

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
        if(Item.Id==0 && string.IsNullOrWhiteSpace(NovaSenha))
            ModelState.AddModelError(nameof(NovaSenha), "Informe a senha inicial.");
        if(!string.IsNullOrWhiteSpace(NovaSenha) && NovaSenha.Length<8)
            ModelState.AddModelError(nameof(NovaSenha), "A senha deve ter ao menos 8 caracteres.");
        if(!ModelState.IsValid) { await OnGetAsync(Item.Id==0?null:Item.Id); return Page(); }
        Item.Email=Item.Email.Trim().ToLowerInvariant();
        try
        {
            await repository.SalvarUsuarioAsync(Item, string.IsNullOrWhiteSpace(NovaSenha)?null:PasswordHasher.Hash(NovaSenha));
        }
        catch(SqlException ex) when (ex.Number is 2601 or 2627)
        {
            ModelState.AddModelError("Item.Email", "Já existe um usuário com este e-mail.");
            await OnGetAsync(Item.Id==0?null:Item.Id);
            return Page();
        }
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