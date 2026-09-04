// ThemeCatalog — semantic-token preview themes (v0.9.3+).
//
// 7 themes × light/dark boards = 14 palettes. Each board fills the 30 CSS
// tokens consumed by style-themes.css; GenerateCss() emits the `:root` block
// and prepends it to the structural sheet, so one stylesheet serves every
// theme. Each theme also maps to a syntect highlight class (FFI bits 7-9,
// 0 = auto legacy pair) so code-block colors follow the preview palette.
//
// Palette sources (official where they exist):
//   Nord        https://www.nordtheme.com/docs/colors-and-palettes
//   Gruvbox     https://github.com/morhetz/gruvbox (gruvbox palette)
//   Everforest  https://github.com/sainnhe/everforest/blob/master/palette.md (medium)
//   Dracula     https://draculatheme.com (dark is official; light derived)
//   Catppuccin  https://github.com/catppuccin/catppuccin (Latte / Mocha)
//   Obsidian    base theme derivation
//   Default     GitHub light/dark (= legacy style.css / style-dark.css values)

using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace NppMarkdownPanel
{
    /// <summary>One light/dark board of the 30 semantic CSS tokens.</summary>
    public sealed class ThemePalette
    {
        public string Bg;
        public string Fg;
        public string HeadingFg;
        public string FgMuted;
        public string Link;
        public string LinkDanger;
        public string Border;
        public string Surface;
        public string TableBorder;
        public string TableRowBg;
        public string BlockquoteBorder;
        public string InlineCodeBg;
        public string PreBg;
        public string PreBorder;
        public string ColorNote;
        public string ColorTip;
        public string ColorWarning;
        public string ColorSevere;
        public string ColorCaution;
        public string ColorImportant;
        public string OutlineBg;
        public string OutlineBorder;
        public string OutlineHoverBg;
        public string OutlineHoverFg;
        public string OutlineActiveBg;
        public string OutlineActiveFg;
        public string OutlineActiveBorder;
        public string ScrollbarThumb;
        public string ScrollbarThumbHover;
        public string SelectionBg;
    }

    /// <summary>A preview theme: two boards + the syntect classes they pair with.</summary>
    public sealed class PreviewThemeDef
    {
        public string Key;              // persisted in the ini file
        public string MenuLabel;        // plugin menu text
        public ThemePalette Light;
        public ThemePalette Dark;
        /// <summary>syntect highlight class for FFI bits 7-9 (0 = auto legacy pair).</summary>
        public byte LightHighlightClass;
        public byte DarkHighlightClass;

        public bool IsDefault => Key == ThemeCatalog.DefaultKey;
    }

    public static class ThemeCatalog
    {
        public const string DefaultKey = "Default";
        private const string ThemeSheetFileName = "style-themes.css";

        /// <summary>Theme menu order; index = plugin menu item index offset.</summary>
        public static readonly PreviewThemeDef[] Themes = BuildThemes();

        /// <summary>Resolve a persisted theme key (case-insensitive, Default on miss).</summary>
        public static PreviewThemeDef Find(string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                foreach (var t in Themes)
                {
                    if (string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase))
                        return t;
                }
            }
            return Themes[0];
        }

        /// <summary>syntect highlight class for a theme/dark combination.</summary>
        public static byte HighlightClass(PreviewThemeDef theme, bool dark)
        {
            if (theme == null || theme.IsDefault) return 0; // legacy auto pair
            return dark ? theme.DarkHighlightClass : theme.LightHighlightClass;
        }

        private static string cachedSheet;

        /// <summary>
        /// Full stylesheet for a theme: the `:root` token block followed by the
        /// shared structural sheet (style-themes.css). Returns null when the
        /// sheet is not deployed — the caller falls back to the legacy CSS.
        /// </summary>
        public static string GenerateCss(PreviewThemeDef theme, bool dark)
        {
            var sheet = LoadThemeSheet();
            if (sheet == null) return null;

            var p = dark ? theme.Dark : theme.Light;
            var sb = new StringBuilder(2048 + sheet.Length);
            sb.Append(":root {\n");
            Append(sb, "--bg", p.Bg);
            Append(sb, "--fg", p.Fg);
            Append(sb, "--heading-fg", p.HeadingFg);
            Append(sb, "--fg-muted", p.FgMuted);
            Append(sb, "--link", p.Link);
            Append(sb, "--link-danger", p.LinkDanger);
            Append(sb, "--border", p.Border);
            Append(sb, "--surface", p.Surface);
            Append(sb, "--table-border", p.TableBorder);
            Append(sb, "--table-row-bg", p.TableRowBg);
            Append(sb, "--blockquote-border", p.BlockquoteBorder);
            Append(sb, "--inline-code-bg", p.InlineCodeBg);
            Append(sb, "--pre-bg", p.PreBg);
            Append(sb, "--pre-border", p.PreBorder);
            Append(sb, "--color-note", p.ColorNote);
            Append(sb, "--color-tip", p.ColorTip);
            Append(sb, "--color-warning", p.ColorWarning);
            Append(sb, "--color-severe", p.ColorSevere);
            Append(sb, "--color-caution", p.ColorCaution);
            Append(sb, "--color-important", p.ColorImportant);
            Append(sb, "--outline-bg", p.OutlineBg);
            Append(sb, "--outline-border", p.OutlineBorder);
            Append(sb, "--outline-hover-bg", p.OutlineHoverBg);
            Append(sb, "--outline-hover-fg", p.OutlineHoverFg);
            Append(sb, "--outline-active-bg", p.OutlineActiveBg);
            Append(sb, "--outline-active-fg", p.OutlineActiveFg);
            Append(sb, "--outline-active-border", p.OutlineActiveBorder);
            Append(sb, "--scrollbar-thumb", p.ScrollbarThumb);
            Append(sb, "--scrollbar-thumb-hover", p.ScrollbarThumbHover);
            Append(sb, "--selection-bg", p.SelectionBg);
            sb.Append("}\n");
            sb.Append(sheet);
            return sb.ToString();
        }

        private static void Append(StringBuilder sb, string token, string value)
        {
            sb.Append("    ").Append(token).Append(": ").Append(value ?? "inherit").Append(";\n");
        }

        private static string LoadThemeSheet()
        {
            if (cachedSheet != null) return cachedSheet;
            try
            {
                var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var sheetPath = Path.Combine(assemblyDir ?? ".", ThemeSheetFileName);
                if (File.Exists(sheetPath))
                {
                    cachedSheet = File.ReadAllText(sheetPath);
                }
            }
            catch (IOException)
            {
                // Missing/unreadable sheet — caller falls back to legacy CSS.
            }
            return cachedSheet;
        }

        // ------------------------------------------------------------------
        // 14 boards

        private static PreviewThemeDef[] BuildThemes()
        {
            return new[]
            {
                new PreviewThemeDef
                {
                    Key = DefaultKey,
                    MenuLabel = "Preview &theme: Default",
                    LightHighlightClass = 0,
                    DarkHighlightClass = 0,
                    Light = new ThemePalette
                    {
                        Bg = "#ffffff", Fg = "#1f2328", HeadingFg = "#1f2328", FgMuted = "#586069",
                        Link = "#0969da", LinkDanger = "#cc0000",
                        Border = "#d8dee4", Surface = "#f6f8fa",
                        TableBorder = "rgba(31, 35, 40, 0.15)", TableRowBg = "#ffffff",
                        BlockquoteBorder = "#d0d7de",
                        InlineCodeBg = "rgba(175, 184, 193, 0.2)",
                        PreBg = "#f6f8fa", PreBorder = "transparent",
                        ColorNote = "#0969da", ColorTip = "#1a7f37", ColorWarning = "#9a6700",
                        ColorSevere = "#bc4c00", ColorCaution = "#d1242f", ColorImportant = "#8250df",
                        OutlineBg = "#f6f8fa", OutlineBorder = "#e1e4e8",
                        OutlineHoverBg = "#e1e4e8", OutlineHoverFg = "#24292e",
                        OutlineActiveBg = "#f1f8ff", OutlineActiveFg = "#24292e", OutlineActiveBorder = "#0366d6",
                        ScrollbarThumb = "rgba(110, 118, 129, 0.4)", ScrollbarThumbHover = "rgba(110, 118, 129, 0.7)",
                        SelectionBg = "rgba(9, 105, 218, 0.2)",
                    },
                    Dark = new ThemePalette
                    {
                        Bg = "#0d1117", Fg = "#c9d1d9", HeadingFg = "#e6edf3", FgMuted = "#8b949e",
                        Link = "#58a6ff", LinkDanger = "#33ffff",
                        Border = "#30363d", Surface = "#161b22",
                        TableBorder = "#30363d", TableRowBg = "transparent",
                        BlockquoteBorder = "#3b434b",
                        InlineCodeBg = "rgba(110, 118, 129, 0.4)",
                        PreBg = "#161b22", PreBorder = "#30363d",
                        ColorNote = "#2f81f7", ColorTip = "#3fb950", ColorWarning = "#d29922",
                        ColorSevere = "#db6d28", ColorCaution = "#f85149", ColorImportant = "#a371f7",
                        OutlineBg = "#252525", OutlineBorder = "#3a3a3a",
                        OutlineHoverBg = "#333333", OutlineHoverFg = "#dddddd",
                        OutlineActiveBg = "#1a3350", OutlineActiveFg = "#dddddd", OutlineActiveBorder = "#58a6ff",
                        ScrollbarThumb = "rgba(139, 148, 158, 0.4)", ScrollbarThumbHover = "rgba(139, 148, 158, 0.7)",
                        SelectionBg = "rgba(56, 139, 253, 0.4)",
                    },
                },
                new PreviewThemeDef
                {
                    Key = "Obsidian",
                    MenuLabel = "Preview theme: &Obsidian",
                    LightHighlightClass = 1, DarkHighlightClass = 3,
                    Light = new ThemePalette
                    {
                        Bg = "#ffffff", Fg = "#222222", HeadingFg = "#1a1a1a", FgMuted = "#6e6e6e",
                        Link = "#7c5cff", LinkDanger = "#cc0000",
                        Border = "#dedede", Surface = "#f7f7f7",
                        TableBorder = "rgba(34, 34, 34, 0.12)", TableRowBg = "#ffffff",
                        BlockquoteBorder = "#d4d4d4",
                        InlineCodeBg = "rgba(34, 34, 34, 0.07)",
                        PreBg = "#f7f7f7", PreBorder = "transparent",
                        ColorNote = "#7c5cff", ColorTip = "#0f7b3f", ColorWarning = "#b08500",
                        ColorSevere = "#bc4c00", ColorCaution = "#d1242f", ColorImportant = "#9a5cf5",
                        OutlineBg = "#f7f7f7", OutlineBorder = "#e0e0e0",
                        OutlineHoverBg = "#ededed", OutlineHoverFg = "#1a1a1a",
                        OutlineActiveBg = "#eceafd", OutlineActiveFg = "#1a1a1a", OutlineActiveBorder = "#7c5cff",
                        ScrollbarThumb = "rgba(120, 120, 120, 0.4)", ScrollbarThumbHover = "rgba(120, 120, 120, 0.7)",
                        SelectionBg = "rgba(124, 92, 255, 0.18)",
                    },
                    Dark = new ThemePalette
                    {
                        Bg = "#1e1e1e", Fg = "#dadada", HeadingFg = "#e6e6e6", FgMuted = "#9a9a9a",
                        Link = "#a882ff", LinkDanger = "#ff6b6b",
                        Border = "#3a3a3a", Surface = "#262626",
                        TableBorder = "#3a3a3a", TableRowBg = "transparent",
                        BlockquoteBorder = "#4c4c4c",
                        InlineCodeBg = "rgba(218, 218, 218, 0.08)",
                        PreBg = "#262626", PreBorder = "#3a3a3a",
                        ColorNote = "#9d86ff", ColorTip = "#4fbf8f", ColorWarning = "#cc9d24",
                        ColorSevere = "#ce7c40", ColorCaution = "#e06c6c", ColorImportant = "#bf7af6",
                        OutlineBg = "#252525", OutlineBorder = "#3a3a3a",
                        OutlineHoverBg = "#333333", OutlineHoverFg = "#dddddd",
                        OutlineActiveBg = "#2f3350", OutlineActiveFg = "#dddddd", OutlineActiveBorder = "#a882ff",
                        ScrollbarThumb = "rgba(154, 154, 154, 0.4)", ScrollbarThumbHover = "rgba(154, 154, 154, 0.7)",
                        SelectionBg = "rgba(124, 92, 255, 0.35)",
                    },
                },
                new PreviewThemeDef
                {
                    Key = "Nord",
                    MenuLabel = "Preview theme: &Nord",
                    LightHighlightClass = 1, DarkHighlightClass = 3,
                    Light = new ThemePalette
                    {
                        Bg = "#eceff4", Fg = "#2e3440", HeadingFg = "#2e3440", FgMuted = "#4c566a",
                        Link = "#5e81ac", LinkDanger = "#bf616a",
                        Border = "#d8dee9", Surface = "#e5e9f0",
                        TableBorder = "rgba(46, 52, 64, 0.15)", TableRowBg = "#ffffff",
                        BlockquoteBorder = "#d8dee9",
                        InlineCodeBg = "rgba(76, 86, 106, 0.12)",
                        PreBg = "#e5e9f0", PreBorder = "transparent",
                        ColorNote = "#5e81ac", ColorTip = "#6f9e5a", ColorWarning = "#b39242",
                        ColorSevere = "#c96e59", ColorCaution = "#bf616a", ColorImportant = "#9b7fa8",
                        OutlineBg = "#e5e9f0", OutlineBorder = "#d8dee9",
                        OutlineHoverBg = "#dbe2ec", OutlineHoverFg = "#2e3440",
                        OutlineActiveBg = "#dce4f2", OutlineActiveFg = "#2e3440", OutlineActiveBorder = "#5e81ac",
                        ScrollbarThumb = "rgba(76, 86, 106, 0.35)", ScrollbarThumbHover = "rgba(76, 86, 106, 0.6)",
                        SelectionBg = "rgba(94, 129, 172, 0.2)",
                    },
                    Dark = new ThemePalette
                    {
                        Bg = "#2e3440", Fg = "#d8dee9", HeadingFg = "#e5e9f0", FgMuted = "#8fa1b3",
                        Link = "#88c0d0", LinkDanger = "#bf616a",
                        Border = "#4c566a", Surface = "#3b4252",
                        TableBorder = "#434c5e", TableRowBg = "transparent",
                        BlockquoteBorder = "#434c5e",
                        InlineCodeBg = "rgba(216, 222, 233, 0.1)",
                        PreBg = "#3b4252", PreBorder = "#434c5e",
                        ColorNote = "#81a1c1", ColorTip = "#a3be8c", ColorWarning = "#ebcb8b",
                        ColorSevere = "#d08770", ColorCaution = "#bf616a", ColorImportant = "#b48ead",
                        OutlineBg = "#3b4252", OutlineBorder = "#434c5e",
                        OutlineHoverBg = "#434c5e", OutlineHoverFg = "#eceff4",
                        OutlineActiveBg = "#434c5e", OutlineActiveFg = "#eceff4", OutlineActiveBorder = "#88c0d0",
                        ScrollbarThumb = "rgba(216, 222, 233, 0.15)", ScrollbarThumbHover = "rgba(216, 222, 233, 0.35)",
                        SelectionBg = "rgba(136, 192, 208, 0.25)",
                    },
                },
                new PreviewThemeDef
                {
                    Key = "Gruvbox",
                    MenuLabel = "Preview theme: &Gruvbox",
                    LightHighlightClass = 2, DarkHighlightClass = 4,
                    Light = new ThemePalette
                    {
                        Bg = "#fbf1c7", Fg = "#3c3836", HeadingFg = "#3c3836", FgMuted = "#7c6f64",
                        Link = "#076678", LinkDanger = "#9d0006",
                        Border = "#d5c4a1", Surface = "#f2e5bc",
                        TableBorder = "rgba(60, 56, 54, 0.2)", TableRowBg = "#fdf6e3",
                        BlockquoteBorder = "#bdae93",
                        InlineCodeBg = "rgba(60, 56, 54, 0.08)",
                        PreBg = "#f2e5bc", PreBorder = "transparent",
                        ColorNote = "#076678", ColorTip = "#79740e", ColorWarning = "#b57614",
                        ColorSevere = "#af3a03", ColorCaution = "#9d0006", ColorImportant = "#8f3f71",
                        OutlineBg = "#f2e5bc", OutlineBorder = "#d5c4a1",
                        OutlineHoverBg = "#e9dfc3", OutlineHoverFg = "#282828",
                        OutlineActiveBg = "#d5c4a1", OutlineActiveFg = "#282828", OutlineActiveBorder = "#076678",
                        ScrollbarThumb = "rgba(124, 111, 100, 0.4)", ScrollbarThumbHover = "rgba(124, 111, 100, 0.65)",
                        SelectionBg = "rgba(7, 102, 120, 0.2)",
                    },
                    Dark = new ThemePalette
                    {
                        Bg = "#282828", Fg = "#ebdbb2", HeadingFg = "#fbf1c7", FgMuted = "#928374",
                        Link = "#83a598", LinkDanger = "#fb4934",
                        Border = "#504945", Surface = "#3c3836",
                        TableBorder = "#504945", TableRowBg = "transparent",
                        BlockquoteBorder = "#665c54",
                        InlineCodeBg = "rgba(235, 219, 178, 0.1)",
                        PreBg = "#3c3836", PreBorder = "#504945",
                        ColorNote = "#83a598", ColorTip = "#b8bb26", ColorWarning = "#fabd2f",
                        ColorSevere = "#fe8019", ColorCaution = "#fb4934", ColorImportant = "#d3869b",
                        OutlineBg = "#32302f", OutlineBorder = "#504945",
                        OutlineHoverBg = "#3c3836", OutlineHoverFg = "#ebdbb2",
                        OutlineActiveBg = "#504945", OutlineActiveFg = "#ebdbb2", OutlineActiveBorder = "#83a598",
                        ScrollbarThumb = "rgba(146, 131, 116, 0.4)", ScrollbarThumbHover = "rgba(146, 131, 116, 0.65)",
                        SelectionBg = "rgba(131, 165, 152, 0.25)",
                    },
                },
                new PreviewThemeDef
                {
                    Key = "Everforest",
                    MenuLabel = "Preview theme: &Everforest",
                    LightHighlightClass = 2, DarkHighlightClass = 6,
                    Light = new ThemePalette
                    {
                        Bg = "#fdf6e3", Fg = "#5c6a72", HeadingFg = "#5c6a72", FgMuted = "#829181",
                        Link = "#3a94c5", LinkDanger = "#f85552",
                        Border = "#edeada", Surface = "#f2efdf",
                        TableBorder = "rgba(92, 106, 114, 0.2)", TableRowBg = "#fffbef",
                        BlockquoteBorder = "#a6b0a0",
                        InlineCodeBg = "rgba(92, 106, 114, 0.1)",
                        PreBg = "#f2efdf", PreBorder = "transparent",
                        ColorNote = "#3a94c5", ColorTip = "#8da101", ColorWarning = "#dfa000",
                        ColorSevere = "#f57d26", ColorCaution = "#f85552", ColorImportant = "#df69ba",
                        OutlineBg = "#f2efdf", OutlineBorder = "#e8e5d5",
                        OutlineHoverBg = "#edeada", OutlineHoverFg = "#5c6a72",
                        OutlineActiveBg = "#ecf5ed", OutlineActiveFg = "#5c6a72", OutlineActiveBorder = "#3a94c5",
                        ScrollbarThumb = "rgba(130, 145, 129, 0.4)", ScrollbarThumbHover = "rgba(130, 145, 129, 0.65)",
                        SelectionBg = "rgba(58, 148, 197, 0.2)",
                    },
                    Dark = new ThemePalette
                    {
                        Bg = "#2d353b", Fg = "#d3c6aa", HeadingFg = "#d3c6aa", FgMuted = "#9da9a0",
                        Link = "#7fbbb3", LinkDanger = "#e67e80",
                        Border = "#475258", Surface = "#343f44",
                        TableBorder = "#3d484d", TableRowBg = "transparent",
                        BlockquoteBorder = "#475258",
                        InlineCodeBg = "rgba(211, 198, 170, 0.08)",
                        PreBg = "#343f44", PreBorder = "#3d484d",
                        ColorNote = "#7fbbb3", ColorTip = "#a7c080", ColorWarning = "#dbbc7f",
                        ColorSevere = "#e69875", ColorCaution = "#e67e80", ColorImportant = "#d699b6",
                        OutlineBg = "#343f44", OutlineBorder = "#3d484d",
                        OutlineHoverBg = "#3d484d", OutlineHoverFg = "#d3c6aa",
                        OutlineActiveBg = "#3a515d", OutlineActiveFg = "#d3c6aa", OutlineActiveBorder = "#7fbbb3",
                        ScrollbarThumb = "rgba(157, 169, 160, 0.35)", ScrollbarThumbHover = "rgba(157, 169, 160, 0.6)",
                        SelectionBg = "rgba(127, 187, 179, 0.25)",
                    },
                },
                new PreviewThemeDef
                {
                    Key = "Dracula",
                    MenuLabel = "Preview theme: &Dracula",
                    LightHighlightClass = 1, DarkHighlightClass = 5,
                    Light = new ThemePalette
                    {
                        Bg = "#f8f8f2", Fg = "#21222c", HeadingFg = "#282a36", FgMuted = "#6272a4",
                        Link = "#8b5cf6", LinkDanger = "#d63447",
                        Border = "#e3e3d6", Surface = "#f0f0e6",
                        TableBorder = "rgba(40, 42, 54, 0.15)", TableRowBg = "#fdfdf8",
                        BlockquoteBorder = "#d4d4c8",
                        InlineCodeBg = "rgba(40, 42, 54, 0.08)",
                        PreBg = "#f0f0e6", PreBorder = "transparent",
                        ColorNote = "#2b7fd0", ColorTip = "#23a55a", ColorWarning = "#b58900",
                        ColorSevere = "#c96a10", ColorCaution = "#d63447", ColorImportant = "#9a6ee0",
                        OutlineBg = "#f0f0e6", OutlineBorder = "#e3e3d6",
                        OutlineHoverBg = "#e6e6da", OutlineHoverFg = "#21222c",
                        OutlineActiveBg = "#e0d9f8", OutlineActiveFg = "#21222c", OutlineActiveBorder = "#8b5cf6",
                        ScrollbarThumb = "rgba(98, 114, 164, 0.4)", ScrollbarThumbHover = "rgba(98, 114, 164, 0.65)",
                        SelectionBg = "rgba(139, 92, 246, 0.2)",
                    },
                    Dark = new ThemePalette
                    {
                        Bg = "#282a36", Fg = "#f8f8f2", HeadingFg = "#f8f8f2", FgMuted = "#6272a4",
                        Link = "#bd93f9", LinkDanger = "#ff5555",
                        Border = "#44475a", Surface = "#21222c",
                        TableBorder = "#44475a", TableRowBg = "transparent",
                        BlockquoteBorder = "#6272a4",
                        InlineCodeBg = "rgba(248, 248, 242, 0.08)",
                        PreBg = "#21222c", PreBorder = "#44475a",
                        ColorNote = "#8be9fd", ColorTip = "#50fa7b", ColorWarning = "#f1fa8c",
                        ColorSevere = "#ffb86c", ColorCaution = "#ff5555", ColorImportant = "#ff79c6",
                        OutlineBg = "#21222c", OutlineBorder = "#3d4152",
                        OutlineHoverBg = "#44475a", OutlineHoverFg = "#f8f8f2",
                        OutlineActiveBg = "#44475a", OutlineActiveFg = "#f8f8f2", OutlineActiveBorder = "#bd93f9",
                        ScrollbarThumb = "rgba(98, 114, 164, 0.5)", ScrollbarThumbHover = "rgba(98, 114, 164, 0.8)",
                        SelectionBg = "rgba(189, 147, 249, 0.3)",
                    },
                },
                new PreviewThemeDef
                {
                    Key = "Catppuccin",
                    MenuLabel = "Preview theme: &Catppuccin",
                    LightHighlightClass = 1, DarkHighlightClass = 3,
                    Light = new ThemePalette
                    {
                        Bg = "#eff1f5", Fg = "#4c4f69", HeadingFg = "#4c4f69", FgMuted = "#6c6f85",
                        Link = "#1e66f5", LinkDanger = "#d20f39",
                        Border = "#ccd0da", Surface = "#e6e9ef",
                        TableBorder = "rgba(76, 79, 105, 0.18)", TableRowBg = "#ffffff",
                        BlockquoteBorder = "#bcc2cc",
                        InlineCodeBg = "rgba(76, 79, 105, 0.1)",
                        PreBg = "#e6e9ef", PreBorder = "transparent",
                        ColorNote = "#1e66f5", ColorTip = "#40a02b", ColorWarning = "#df8e1d",
                        ColorSevere = "#fe640b", ColorCaution = "#d20f39", ColorImportant = "#8839ef",
                        OutlineBg = "#e6e9ef", OutlineBorder = "#ccd0da",
                        OutlineHoverBg = "#dce0e8", OutlineHoverFg = "#4c4f69",
                        OutlineActiveBg = "#ccd0da", OutlineActiveFg = "#4c4f69", OutlineActiveBorder = "#1e66f5",
                        ScrollbarThumb = "rgba(140, 143, 161, 0.4)", ScrollbarThumbHover = "rgba(140, 143, 161, 0.65)",
                        SelectionBg = "rgba(30, 102, 245, 0.2)",
                    },
                    Dark = new ThemePalette
                    {
                        Bg = "#1e1e2e", Fg = "#cdd6f4", HeadingFg = "#cdd6f4", FgMuted = "#a6adc8",
                        Link = "#89b4fa", LinkDanger = "#f38ba8",
                        Border = "#313244", Surface = "#181825",
                        TableBorder = "#313244", TableRowBg = "transparent",
                        BlockquoteBorder = "#45475a",
                        InlineCodeBg = "rgba(205, 214, 244, 0.08)",
                        PreBg = "#181825", PreBorder = "#313244",
                        ColorNote = "#89b4fa", ColorTip = "#a6e3a1", ColorWarning = "#f9e2af",
                        ColorSevere = "#fab387", ColorCaution = "#f38ba8", ColorImportant = "#cba6f7",
                        OutlineBg = "#181825", OutlineBorder = "#313244",
                        OutlineHoverBg = "#313244", OutlineHoverFg = "#cdd6f4",
                        OutlineActiveBg = "#313244", OutlineActiveFg = "#cdd6f4", OutlineActiveBorder = "#89b4fa",
                        ScrollbarThumb = "rgba(147, 153, 178, 0.35)", ScrollbarThumbHover = "rgba(147, 153, 178, 0.6)",
                        SelectionBg = "rgba(137, 180, 250, 0.25)",
                    },
                },
            };
        }
    }
}
