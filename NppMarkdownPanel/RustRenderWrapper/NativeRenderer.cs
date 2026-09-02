// RustRenderWrapper — Native P/Invoke bridge to rustrender.dll (route A').
//
// Maps the v4.0 FFI contract onto .NET Framework 4.7.2:
//   render_markdown(md, md_len, cwd, cwd_len, options, out_html, out_len) -> rc
//   free_html(ptr)
//   rustrender_version() -> const char*
//
// Return codes: 0 OK, 1 invalid input, 2 panic caught, 3 render error,
// 4 null pointer. On success *out_html is a NUL-terminated UTF-8 buffer
// allocated by Rust — the caller MUST free it via FreeHtml.

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace RustRenderWrapper
{
    /// <summary>FFI option bit flags (bit order = v4.0 contract).</summary>
    [Flags]
    public enum RenderFlags : uint
    {
        None = 0,
        DarkMode = 1u << 0,
        EnableCallout = 1u << 1,
        EnableWikilink = 1u << 2,
        EnableMermaid = 1u << 3,
        EnableKatex = 1u << 4,
        SourceLineAnchors = 1u << 5,
        Highlight = 1u << 6,
        Defaults = EnableCallout | EnableWikilink | EnableMermaid | EnableKatex
                 | SourceLineAnchors | Highlight,
    }

    /// <summary>
    /// Owns the Rust-allocated HTML buffer. Disposing calls <c>free_html</c>
    /// (Rust allocator) — never free with a .NET allocator.
    /// </summary>
    internal sealed class HtmlBuffer : SafeHandle
    {
        public HtmlBuffer(IntPtr ptr) : base(IntPtr.Zero, true)
        {
            SetHandle(ptr);
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        /// <summary>Decode the NUL-terminated UTF-8 payload of length <paramref name="len"/>.</summary>
        public string ToString(int len)
        {
            if (IsInvalid || len <= 0) return string.Empty;
            var bytes = new byte[len];
            Marshal.Copy(handle, bytes, 0, len);
            return Encoding.UTF8.GetString(bytes);
        }

        protected override bool ReleaseHandle()
        {
            FreeHtml(handle);
            return true;
        }
    }

    internal static class NativeMethods
    {
        private const string DllName = "rustrender";

        private static readonly byte[] EmptyBytes = new byte[0];

        [DllImport(DllName, EntryPoint = "render_markdown", CallingConvention = CallingConvention.Cdecl)]
        private static extern int RenderMarkdownNative(
            byte[] md, UIntPtr mdLen,
            byte[] cwd, UIntPtr cwdLen,
            uint options,
            out IntPtr outHtml, out UIntPtr outLen);

        [DllImport(DllName, EntryPoint = "free_html", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void FreeHtml(IntPtr ptr);

        [DllImport(DllName, EntryPoint = "rustrender_version", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr RustrenderVersionNative();

        /// <summary>Human-readable renderer version (empty when DLL missing).</summary>
        internal static string Version()
        {
            try
            {
                return Marshal.PtrToStringAnsi(RustrenderVersionNative()) ?? string.Empty;
            }
            catch (DllNotFoundException) { return string.Empty; }
            catch (EntryPointNotFoundException) { return string.Empty; }
        }

        /// <summary>
        /// Render markdown. Throws <see cref="NativeRenderException"/> on any
        /// non-zero return code; panics inside Rust can never crash the host
        /// (code 2) — they surface as exceptions here.
        /// </summary>
        internal static string RenderMarkdown(string markdown, string documentDir, RenderFlags flags)
        {
            byte[] md = Encoding.UTF8.GetBytes(markdown);
            byte[] cwd = string.IsNullOrEmpty(documentDir)
                ? EmptyBytes
                : Encoding.UTF8.GetBytes(documentDir);

            IntPtr htmlPtr;
            UIntPtr htmlLen;
            int rc = RenderMarkdownNative(
                md, (UIntPtr)md.LongLength,
                cwd, (UIntPtr)cwd.LongLength,
                (uint)flags,
                out htmlPtr, out htmlLen);

            if (rc != 0)
            {
                throw new NativeRenderException(rc);
            }

            using (var buf = new HtmlBuffer(htmlPtr))
            {
                return buf.ToString((int)htmlLen);
            }
        }
    }

    /// <summary>Non-zero return code from rustrender.dll.</summary>
    [Serializable]
    public sealed class NativeRenderException : Exception
    {
        public int ReturnCode { get; }

        internal NativeRenderException(int rc)
            : base(MakeMessage(rc))
        {
            ReturnCode = rc;
        }

        private static string MakeMessage(int rc)
        {
            switch (rc)
            {
                case 1: return "rustrender: invalid input (invalid UTF-8 or embedded NUL).";
                case 2: return "rustrender: a rendering panic was contained (hostile document).";
                case 3: return "rustrender: rendering failed.";
                case 4: return "rustrender: null pointer argument.";
                default: return "rustrender: unknown error code " + rc + ".";
            }
        }
    }
}
