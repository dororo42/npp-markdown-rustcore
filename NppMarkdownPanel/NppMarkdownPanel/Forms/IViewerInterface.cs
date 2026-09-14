using NppMarkdownPanel.Entities;
using System;

namespace NppMarkdownPanel.Forms
{
    /// <summary>HTML export mode (v1.2.3).</summary>
    public enum HtmlExportMode
    {
        /// <summary>Plain: image references are kept as-is.</summary>
        Plain = 0,
        /// <summary>EmbedBase64: local images baked in as data URIs (single file).</summary>
        EmbedBase64 = 1,
        /// <summary>LocalImages: images copied next to the HTML, src rewritten to relative paths (Calibre/EPUB friendly).</summary>
        LocalImages = 2,
    }

    public interface IViewerInterface
    {
        IntPtr Handle { get; }
        void InitRenderingEngine(Settings newSettings, Action<string> openLocalFileInNppAction);
        void SetMarkdownFilePath(string filepath, bool isRename = false);
        void UpdateSettings(Settings settings, Action<string> openLocalFileInNppAction);
        void RenderMarkdown(string currentText, string filepath, bool preserveVerticalScrollPosition = true);
        void ScrollToElementWithLineNo(int lineNo, bool scrollToEnd);
        bool IsValidFileExtension(string filename);
        void Cleanup();
        void ExportToPdf();
        /// <summary>Save-as dialog exporting the current preview as HTML in the given mode.</summary>
        void ExportToHtml(HtmlExportMode mode);
    }
}
