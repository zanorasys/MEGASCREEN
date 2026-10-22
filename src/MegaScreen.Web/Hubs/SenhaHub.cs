using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using MegaScreen.Web.Services;

namespace MegaScreen.Web.Hubs;

// Hub de tempo real.
// - Equipe logada (painel / profissional): EntrarNaFilial -> recebe "FilaAtualizada" e "SenhaChamada" com dados completos.
// - TV pública: EntrarComoTela -> recebe só "Atualizar" e "Chamada"(id), sem dados de paciente (LGPD).
public class SenhaHub : Hub
{
    public async Task EntrarNaFilial(int filialId)
    {
        var user = Context.User;
        if (user?.Identity?.IsAuthenticated != true)
            throw new HubException("Faça login para acompanhar a fila.");
        if (!user.IsInRole("Admin") && user.FindFirstValue("FilialId") != filialId.ToString())
            throw new HubException("Acesso negado a esta filial.");
        await Groups.AddToGroupAsync(Context.ConnectionId, SenhaService.Grupo(filialId));
    }

    public Task EntrarComoTela(int filialId)
        => Groups.AddToGroupAsync(Context.ConnectionId, SenhaService.GrupoTv(filialId));

    public async Task SairDaFilial(int filialId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, SenhaService.Grupo(filialId));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, SenhaService.GrupoTv(filialId));
    }
}
