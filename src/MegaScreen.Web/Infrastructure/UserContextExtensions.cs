using System.Security.Claims;

namespace MegaScreen.Web.Infrastructure;

public static class UserContextExtensions
{
    public static int FilialId(this ClaimsPrincipal user)
        => int.TryParse(user.FindFirstValue("FilialId"), out var id) ? id : 0;

    public static int UsuarioId(this ClaimsPrincipal user)
        => int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public static string LocalAtual(this ClaimsPrincipal user)
        => user.FindFirstValue("LocalAtual") ?? "Recepção I";
}
