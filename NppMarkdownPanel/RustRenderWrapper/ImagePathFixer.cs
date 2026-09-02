// RustRenderWrapper — defensive post-pass for local image/link paths.
//
// The Rust core resolves relative paths to absolute file:/// URLs during
// rendering (comrak URLRewriter), so this pass is a no-op in the healthy
// native pipeline. It remains for:
//   1. the Markdig fallback when the renderer DLL is missing, and
//   2. documents whose authors wrote absolute-but-unresolvable URLs.

using System;
using System.IO;
using System.Text.RegularExpressions;

namespace RustRenderWrapper
{
    public static class ImagePathFixer
    {
        // Matches src/href attributes whose value does not already carry a
        // scheme (http:, file:, data:, mailto:…) and is not an anchor.
        private static readonly Regex AttrUrl = new Regex(
            "(src|href)\\s*=\\s*([\"\\'])([^\"\\']*)\\2",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Rewrite scheme-less attribute URLs to absolute file:/// URLs.</summary>
        public static string Fix(string html, string documentPath)
        {
            if (string.IsNullOrEmpty(html) || string.IsNullOrEmpty(documentPath))
                return html;

            string baseDir;
            try { baseDir = Path.GetDirectoryName(documentPath); }
            catch (Exception) { return html; }
            if (string.IsNullOrEmpty(baseDir)) return html;

            return AttrUrl.Replace(html, match =>
            {
                string attr = match.Groups[1].Value;
                string quote = match.Groups[2].Value;
                string url = match.Groups[3].Value;

                if (string.IsNullOrWhiteSpace(url) || url.StartsWith("#"))
                    return match.Value;
                if (url.Contains(":"))          // http:, file:, mailto:, …
                    return match.Value;
                if (url.StartsWith("//"))       // scheme-relative
                    return match.Value;

                try
                {
                    string abs = Path.GetFullPath(Path.Combine(baseDir, url));
                    return attr + "=" + quote + "file:///" + abs.Replace('\\', '/') + quote;
                }
                catch (Exception)
                {
                    return match.Value;
                }
            });
        }
    }
}
