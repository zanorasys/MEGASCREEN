namespace MegaScreen.Web.Infrastructure;

/// <summary>
/// Data/hora oficial do sistema: America/Sao_Paulo.
/// Use sempre DataHora.Agora() no lugar de DateTime.Now / GETDATE() e passe como parâmetro (@Agora) nos INSERT/UPDATE.
/// </summary>
public static class DataHora
{
    private static readonly TimeZoneInfo Fuso = ObterFuso();

    private static TimeZoneInfo ObterFuso()
    {
        // .NET 6+ aceita o ID IANA no Windows e no Linux; o ID Windows fica como fallback.
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); }
    }

    /// <summary>Data e hora atual em São Paulo (Kind = Unspecified, pronto para datetime2).</summary>
    public static DateTime Agora() => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Fuso), DateTimeKind.Unspecified);

    /// <summary>Data de hoje em São Paulo (00:00).</summary>
    public static DateTime Hoje() => Agora().Date;
}
