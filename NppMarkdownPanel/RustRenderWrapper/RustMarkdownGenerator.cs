// RustRenderWrapper — IMarkdownGenerator implementation backed by
// rustrender.dll, with transparent fallback to the upstream Markdig pipeline
// when the native DLL is unavailable (deployment safety net).

using System;
using System.IO;
using PanelCommon;

namespace RustRenderWrapper
{
    /// <summary>
    /// Static service surface: builds the active <see cref="IMarkdownGenerator"/>
    /// (Rust-first) and exposes the render flags the host may toggle at runtime
    /// (dark mode re-render, feature toggles from the settings dialog).
    /// </summary>
    public static class RustRenderService
    {
        // Render options are read/written from both the UI thread and the
        // background render Task (and the export lazy-render path), so all
        // access goes through this gate; a render always snapshots a
        // consistent (flags, theme class) pair.
        private static readonly object OptionsGate = new object();
        private static RenderFlags _currentFlags = RenderFlags.Defaults;

        /// <summary>Flags applied to every native render. The host sets
        /// <see cref="RenderFlags.DarkMode"/> before re-rendering on theme change.</summary>
        public static RenderFlags CurrentFlags
        {
            get { lock (OptionsGate) { return _currentFlags; } }
            set { lock (OptionsGate) { _currentFlags = value; } }
        }

        private static byte _currentThemeClass;

        /// <summary>
        /// Syntect highlight theme class (FFI bits 7-9): 0 = auto (the legacy
        /// dark/light pair), 1..=6 pin a palette so code-block colors match the
        /// preview theme.
        /// </summary>
        public static byte CurrentThemeClass
        {
            get { lock (OptionsGate) { return _currentThemeClass; } }
            set { lock (OptionsGate) { _currentThemeClass = value; } }
        }

        /// <summary>True when rustrender.dll loaded successfully.</summary>
        public static bool NativeAvailable { get; }

        /// <summary>rustrender build descriptor ("" when the DLL is missing).</summary>
        public static string NativeVersion { get; }

        static RustRenderService()
        {
            NativeVersion = NativeMethods.Version();
            NativeAvailable = NativeVersion.Length > 0;
        }

        /// <summary>Create the active generator: Rust core when loadable, Markdig otherwise.</summary>
        public static IMarkdownGenerator CreateGenerator()
        {
            return new RustMarkdownGenerator();
        }

        /// <summary>
        /// Atomic snapshot of the effective FFI option word: render flags plus
        /// the highlight theme class encoded in bits 7-9 (v4.0 contract).
        /// </summary>
        internal static uint EffectiveOptions()
        {
            lock (OptionsGate)
            {
                return (uint)_currentFlags | ((uint)_currentThemeClass << 7);
            }
        }
    }

    public sealed class RustMarkdownGenerator : IMarkdownGenerator
    {
        // Fallback pipeline is constructed lazily: only pay the Markdig init
        // cost if the native DLL is actually missing/failing.
        private MarkdigWrapper.MarkdigWrapper _fallback;
        private readonly bool _native;

        public RustMarkdownGenerator()
        {
            _native = RustRenderService.NativeAvailable;
        }

        public string ConvertToHtml(string markDownText, string filepath, bool supportEscapeCharsInUris)
        {
            if (_native)
            {
                try
                {
                    string dir = string.IsNullOrEmpty(filepath)
                        ? null
                        : SafeDir(filepath);

                    // Route C front-ends handle mermaid/katex; the flags are
                    // forwarded unchanged. Native highlighting bakes the theme
                    // matching the flags + theme class snapshot below.
                    return NativeMethods.RenderMarkdown(
                        markDownText, dir, RustRenderService.EffectiveOptions());
                }
                catch (DllNotFoundException)
                {
                    // DLL vanished mid-session — fall through to Markdig.
                }
                catch (NativeRenderException ex) when (
                    ex.ReturnCode == 1 || ex.ReturnCode == 2 || ex.ReturnCode == 3)
                {
                    // Hostile/undecodable document: show a contained error card
                    // instead of crashing or blanking the preview.
                    return ErrorCard(ex.Message);
                }
            }

            return Fallback().ConvertToHtml(markDownText, filepath, supportEscapeCharsInUris);
        }

        private IMarkdownGenerator Fallback()
        {
            return _fallback ?? (_fallback = new MarkdigWrapper.MarkdigWrapper());
        }

        private static string SafeDir(string filepath)
        {
            try { return Path.GetDirectoryName(filepath); }
            catch (Exception) { return null; }
        }

        private static string ErrorCard(string message)
        {
            string esc = System.Net.WebUtility.HtmlEncode(message ?? "unknown error");
            return "<div style=\"border:1px solid #d33;border-radius:6px;padding:12px;"
                 + "margin:16px;font-family:sans-serif;\">"
                 + "<strong>Preview render failed</strong><br/><span style=\"color:#a00;\">"
                 + esc
                 + "</span></div>";
        }
    }
}
