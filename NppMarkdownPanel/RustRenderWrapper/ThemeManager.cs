// RustRenderWrapper — CSS theme management for the preview.
//
// Loads the upstream style.css / style-dark.css from the plugin directory,
// caches them, and appends the code-token stylesheet used by the WASM route
// (front-end highlight.js classes). The native route bakes syntect colors
// inline, so the token CSS is inert there.

using System;
using System.IO;

namespace RustRenderWrapper
{
    public sealed class ThemeManager
    {
        private string _lightCss;
        private string _darkCss;
        private readonly string _pluginDir;

        public ThemeManager(string pluginDir)
        {
            _pluginDir = pluginDir ?? string.Empty;
        }

        /// <summary>CSS for the requested theme (cached after first read).</summary>
        public string GetCss(bool darkMode)
        {
            if (darkMode)
            {
                if (_darkCss == null)
                    _darkCss = ReadFile("style-dark.css") ?? _lightCss ?? string.Empty;
                return _darkCss;
            }

            if (_lightCss == null)
                _lightCss = ReadFile("style.css") ?? string.Empty;
            return _lightCss;
        }

        /// <summary>CSS with the front-end highlight token classes appended.</summary>
        public string GetCssWithCodeTokens(bool darkMode)
        {
            return GetCss(darkMode) + CodeTokenCss;
        }

        private string ReadFile(string name)
        {
            try
            {
                string path = Path.Combine(_pluginDir, name);
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Minimal token palette mirroring syntect's InspiredGitHub accents.
        /// Applied only in the WASM route; class names follow highlight.js
        /// conventions (hljs-keyword, hljs-string, …).
        /// </summary>
        public const string CodeTokenCss = @"
/* rustrender: front-end code token palette (route C) */
.hljs-comment,.hljs-quote{color:#998;font-style:italic}
.hljs-keyword,.hljs-selector-tag,.hljs-subst{color:#333;font-weight:bold}
.hljs-literal,.hljs-number,.hljs-tag{color:#099}
.hljs-string,.hljs-doctag{color:#d14}
.hljs-title,.hljs-section,.hljs-name{color:#900;font-weight:bold}
.hljs-type,.hljs-class .hljs-title{color:#458;font-weight:bold}
.hljs-attribute,.hljs-variable,.hljs-template-variable{color:#008080}
.hljs-built_in,.hljs-builtin-name{color:#0086b3}
.hljs-meta{color:#999}
.hljs-emphasis{font-style:italic}
.hljs-strong{font-weight:bold}
";
    }
}
