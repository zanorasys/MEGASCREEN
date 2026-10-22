using System.Text.RegularExpressions;

namespace MegaScreen.Web.Infrastructure;

/// <summary>Converte links do YouTube/Vimeo em URL de embed. Qualquer outro link é rejeitado (retorna null).</summary>
public static partial class VideoLink
{
    [GeneratedRegex(@"^https?://(?:www\.|m\.)?(?:youtube\.com/(?:watch\?(?:.*&)?v=|embed/|shorts/|live/)|youtu\.be/)([A-Za-z0-9_-]{11})", RegexOptions.IgnoreCase)]
    private static partial Regex YouTube();

    [GeneratedRegex(@"^https?://(?:www\.|player\.)?vimeo\.com/(?:video/)?(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex Vimeo();

    public static bool Valido(string? url) => Embed(url) is not null;

    public static string? Embed(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        url = url.Trim();
        var yt = YouTube().Match(url);
        if (yt.Success)
        {
            var id = yt.Groups[1].Value;
            return $"https://www.youtube-nocookie.com/embed/{id}?autoplay=1&mute=1&loop=1&playlist={id}&controls=0&rel=0&modestbranding=1&playsinline=1";
        }
        var vm = Vimeo().Match(url);
        if (vm.Success)
            return $"https://player.vimeo.com/video/{vm.Groups[1].Value}?autoplay=1&muted=1&loop=1&background=1";
        return null;
    }
}
