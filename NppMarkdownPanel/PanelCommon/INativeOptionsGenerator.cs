using System;

namespace PanelCommon
{
    /// <summary>
    /// Optional capability interface for generators that can render with an
    /// explicitly supplied native option word instead of the process-wide
    /// snapshot (RustRenderService.EffectiveOptions()).
    /// </summary>
    /// <remarks>
    /// Used by the export paths: the light-theme export must bake its code
    /// blocks with the light syntect theme while the preview snapshot holds
    /// the dark one. Passing the option word explicitly removes any need to
    /// transiently flip shared state. Generators without native options
    /// (Markdig fallback) simply don't implement this — the caller falls
    /// back to the plain <see cref="IMarkdownGenerator.ConvertToHtml"/>.
    /// </remarks>
    public interface INativeOptionsGenerator : IMarkdownGenerator
    {
        /// <summary>
        /// Converts the markdown text to html using the given native option
        /// word (render flags in bits 0-6, syntect highlight class in bits
        /// 7-9). Ignored by managed-only pipelines.
        /// </summary>
        string ConvertToHtmlWithOptions(string markDownText, string filepath, bool supportEscapeCharsInUris, uint nativeOptions);
    }
}
