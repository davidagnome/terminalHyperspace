using Avalonia.Media;

namespace TerminalHyperspace.UI;

/// Reusable FontFamily references for every font under /Fonts.
/// Mirrored as XAML resources in App.axaml — keep both in sync.
public static class Fonts
{
    private const string Base = "avares://TerminalHyperspace/Fonts/";

    // DejaVu Sans Mono (terminal output, command/input boxes, map labels, character overview)
    public static readonly FontFamily MonoRegular     = new(Base + "DejaVuSansMono.ttf#DejaVu Sans Mono");
    public static readonly FontFamily MonoBold        = new(Base + "DejaVuSansMono-Bold.ttf#DejaVu Sans Mono");
    public static readonly FontFamily MonoOblique     = new(Base + "DejaVuSansMono-Oblique.ttf#DejaVu Sans Mono");
    public static readonly FontFamily MonoBoldOblique = new(Base + "DejaVuSansMono-BoldOblique.ttf#DejaVu Sans Mono");

    // DINish (proportional)
    public static readonly FontFamily DINishRegular    = new(Base + "DINish-Regular.otf#DINish");
    public static readonly FontFamily DINishBold       = new(Base + "DINish-Bold.otf#DINish");
    public static readonly FontFamily DINishItalic     = new(Base + "DINish-Italic.otf#DINish");
    public static readonly FontFamily DINishBoldItalic = new(Base + "DINish-BoldItalic.otf#DINish");

    // DINish Condensed (decorative / sidebar accents when desired)
    public static readonly FontFamily DINishCondensedRegular    = new(Base + "DINishCondensed-Regular.otf#DINish Condensed");
    public static readonly FontFamily DINishCondensedBold       = new(Base + "DINishCondensed-Bold.otf#DINish Condensed");
    public static readonly FontFamily DINishCondensedItalic     = new(Base + "DINishCondensed-Italic.otf#DINish Condensed");
    public static readonly FontFamily DINishCondensedBoldItalic = new(Base + "DINishCondensed-BoldItalic.otf#DINish Condensed");
}
