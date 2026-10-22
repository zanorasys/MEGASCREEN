using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using MegaScreen.Web.Services;

namespace MegaScreen.Web.Hubs;

// Hub de tempo real: painel do atendente, TV pública e tela do profissional
// entram no grupo da filial e recebem "FilaAtualizada" / "UltimasChamadas" / "SenhaChamada".
public class SenhaHub : Hub
{
    public async Task EntrarNaFilial(int filialId)
    {
        // Usuário logado só entra na própria filial (Admin pode qualquer uma).
        // TV e totem (anônimos) continuam entrando pela filial da URL.
        var user = Context.User;
        if (user?.Identity?.IsAuthenticated == true && !user.IsInRole("Admin"))
        {
            var minhaFilial = user.FindFirstValue("FilialId");
            if (minhaFilial != filialId.ToString())
                throw new HubException("Acesso negado a esta filial.");
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, SenhaService.Grupo(filialId));
    }

    public async Task SairDaFilial(int filialId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, SenhaService.Grupo(filialId));
    }
}
