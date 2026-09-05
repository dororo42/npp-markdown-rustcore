using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NppMarkdownPanel.Entities
{
    public class Settings
    {
        public const string DefaultCssFile = "style.css";
        public const string DefaultDarkModeCssFile = "style-dark.css";
        public const string DEFAULT_SUPPORTED_FILE_EXT = "md,mkd,mdwn,mdown,mdtxt,markdown,txt";

        public const string RENDERING_ENGINE_WEBVIEW1_IE11 = "IE11";
        public const string RENDERING_ENGINE_WEBVIEW2_EDGE = "EDGE";

        public Settings()
        {
            RenderingEngine = RENDERING_ENGINE_WEBVIEW2_EDGE;
            PreviewTheme = ThemeCatalog.DefaultKey;
            PreviewDarkMode = DarkModeAuto;
        }

        /// <summary>Preview dark-mode board selection: 0 = follow the editor, 1 = force light, 2 = force dark.</summary>
        public const int DarkModeAuto = 0;
        public const int DarkModeForceLight = 1;
        public const int DarkModeForceDark = 2;

        public string CssFileName { get; set; }
        public string CssDarkModeFileName { get; set; }
        public int ZoomLevel { get; set; }
        public string HtmlFileName { get; set; }
        public string SupportedFileExt { get; set; }
        public bool SupportFilesWithNoExt { get; set; }
        public bool AllowAllExtensions { get; set; }
        public bool IsDarkModeEnabled { get; set; }
        public bool ShowToolbar { get; set; }
        public bool ShowStatusbar { get; set; }
        public bool AutoShowPanel { get; set; }
        public bool EnableThreeStateToggle { get; set; }
        public bool ShowOutline { get; set; }

        /// <summary>Preview color theme key (ThemeCatalog: Default/Obsidian/Nord/...).</summary>
        public string PreviewTheme { get; set; }

        /// <summary>
        /// Which board (light/dark) of the selected theme to render:
        /// DarkModeAuto follows the editor dark mode; ForceLight/ForceDark pin
        /// a board so e.g. a light editor can still preview dark palettes
        /// (replaces the old binary FollowDarkMode flag).
        /// </summary>
        public int PreviewDarkMode { get; set; }

        /// <summary>Effective dark-board decision for rendering.</summary>
        public bool IsDarkBoard()
        {
            return PreviewDarkMode == DarkModeForceDark
                || (PreviewDarkMode == DarkModeAuto && IsDarkModeEnabled);
        }

        public string PreProcessorCommandFilename { get; set; }
        public string PreProcessorArguments { get; set; }
        public string PostProcessorCommandFilename { get; set; }
        public string PostProcessorArguments { get; set; }

        public string RenderingEngine { get; set; }

        public bool IsRenderingEngineIE11()
        {
            return RenderingEngine == RENDERING_ENGINE_WEBVIEW1_IE11;
        }

        public bool IsRenderingEngineEdge()
        {
            return RenderingEngine == RENDERING_ENGINE_WEBVIEW2_EDGE;
        }

    }
}
