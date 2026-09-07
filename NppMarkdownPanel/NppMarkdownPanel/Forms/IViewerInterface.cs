using NppMarkdownPanel.Entities;
using System;

namespace NppMarkdownPanel.Forms
{
    public interface IViewerInterface
    {
        IntPtr Handle { get; }
        void InitRenderingEngine(Settings newSettings, Action<string> openLocalFileInNppAction);
        void SetMarkdownFilePath(string filepath, bool isRename = false);
        void UpdateSettings(Settings settings, Action<string> openLocalFileInNppAction);
        void RenderMarkdown(string currentText, string filepath, bool preserveVerticalScrollPosition = true);
        void ScrollToElementWithLineNo(int lineNo);
        bool IsValidFileExtension(string filename);
        void Cleanup();
        void ExportToPdf();
        /// <summary>Save-as dialog exporting the current preview as HTML; embedImages bakes local images in as base64 data URIs (single file).</summary>
        void ExportToHtml(bool embedImages);
    }
}
