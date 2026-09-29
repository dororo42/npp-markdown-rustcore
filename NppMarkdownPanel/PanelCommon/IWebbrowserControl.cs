using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PanelCommon
{
    public interface IWebbrowserControl
    {
        void Initialize(int zoomLevel, Action<string> openLocalFileInNppAction);
        void AddToHost(Control host);
        void PrepareContentUpdate(bool preserveVerticalScrollPosition);
        void SetContent(string content, string body, string style, string currentDocumentPath);
        void CurrentDocumentRenamed(string newDocumentPath);
        void SetZoomLevel(int zoomLevel);
        void ScrollToElementWithLineNo(int lineNo, bool scrollToEnd);
        string GetRenderingEngineName();

        Bitmap MakeScreenshot();

        Action<string> StatusTextChangedAction { get; set; }
        Action RenderingDoneAction { get; set; }
        Action AfterInitCompletedAction { get; set; }
        Action<int> CheckboxToggleAction { get; set; }
        /// <summary>
        /// Preview scrolled: reports the source line (data-line) currently at
        /// the viewport top. <paramref name="atBottom"/> is true when the
        /// preview reached the end of the page (bottom lock for reverse sync).
        /// </summary>
        Action<int, bool> PreviewScrollAction { get; set; }
        Action<int> RadioToggleAction { get; set; }

        void Dispose();

        bool IsInitialized();

        void StopScrollPositionTracking();

        void ExportToPdf(string filePath);

        /// <summary>
        /// Gate for the host↔page message bridge (checkbox/radio/scroll
        /// callbacks) and the injected bridge scripts. Must be set to
        /// <c>false</c> before rendering foreign HTML documents (HTML source
        /// preview): their scripts must neither install the bridge handlers
        /// nor have their postMessage traffic honored by the host.
        /// </summary>
        bool WebBridgeEnabled { get; set; }
    }
}
