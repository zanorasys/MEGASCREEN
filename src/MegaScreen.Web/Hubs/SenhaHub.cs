using Microsoft.AspNetCore.SignalR;
using MegaScreen.Web.Services;

namespace MegaScreen.Web.Hubs;

// Hub de tempo real: painel do atendente, TV pública e tela do profissional
// entram no grupo da filial e recebem "FilaAtualizada" / "SenhaChamada".
public class SenhaHub : Hub
{
    public async Task EntrarNaFilial(int filialId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, SenhaService.Grupo(filialId));
    }

    public async Task SairDaFilial(int filialId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, SenhaService.Grupo(filialId));
    }
}
