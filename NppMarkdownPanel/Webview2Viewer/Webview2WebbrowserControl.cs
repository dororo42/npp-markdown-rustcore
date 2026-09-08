using Microsoft.Web.WebView2.Core;
using PanelCommon;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace Webview2Viewer
{
    public class Webview2WebbrowserControl : IWebbrowserControl, IDisposable
    {
        const string virtualHostProtocol = "http://";
        const string virtualHostName = "markdownpanel-virtualhost";

        // Large-document delivery: NavigateToString has a 2 MB htmlContent
        // cap (Microsoft-documented). A ~1 MB Chinese markdown with syntax
        // highlighting expands past it and the navigation silently fails
        // (blank preview). Oversized pages are written to a temp folder and
        // delivered via a dedicated virtual host instead — the recommended
        // workaround, which is also faster for huge documents.
        const int NavigateToStringMaxChars = 750_000;
        const string tmpVirtualHostName = "markdownpanel-tmp";
        string tmpHtmlDir;
        int tmpHtmlFileCounter;
        const string CONFIG_FOLDER_NAME = "MarkdownPanel";
        private Microsoft.Web.WebView2.WinForms.WebView2 webView;
        private bool webViewInitialized = false;

        public Action<string> StatusTextChangedAction { get; set; }
        public Action RenderingDoneAction { get; set; }
        public Action AfterInitCompletedAction { get; set; }
        public Action<int> CheckboxToggleAction { get; set; }
        public Action<int> RadioToggleAction { get; set; }
        /// <summary>Preview viewport top moved: reports the data-line at the top (bidirectional sync).</summary>
        public Action<int> PreviewScrollAction { get; set; }

        private string currentBody;
        private string currentStyle;

        private string currentDocumentPath;

        private bool currentPageHasOutline;
        private bool forceFullReload;

        private Action<string> openLocalFileInNppAction;

        private CoreWebView2Environment environment = null;
        // EnsureCoreWebView2Async 的任务句柄, Dispose 时有界等待其完成,
        // 避免初始化进行中销毁控件引发竞态异常
        private Task initTask;

        public Webview2WebbrowserControl()
        {
            webView = null;
        }

        public void Dispose()
        {
            var view = webView;
            if (view == null) return;
            webView = null;
            webViewInitialized = false;
            try
            {
                // 1. 先解绑全部事件, 防止关闭/销毁期间回调进入半失效状态
                view.CoreWebView2InitializationCompleted -= WebView_CoreWebView2InitializationCompleted;
                view.NavigationStarting -= OnWebBrowser_NavigationStarting;
                view.NavigationCompleted -= WebView_NavigationCompleted;
                if (view.CoreWebView2 != null)
                {
                    view.CoreWebView2.WebMessageReceived -= WebView_WebMessageReceived;
                }

                // 2. 有界等待初始化完成 (最长 2s)
                initTask?.Wait(TimeSpan.FromSeconds(2));

                // 3. Dispose 释放控件与底层 CoreWebView2Controller (.NET 包装层
                //    无显式 Close API; 浏览器进程依赖宿主存活性监视, 在宿主
                //    退出后自行终止 —— 故下方泵消息确保宿主收尾不被阻断)
                view.Dispose();

                // 4. 有限泵消息: 浏览器进程退出是异步的, 让关闭 IPC 在宿主
                //    退出前送达, 否则 msedgewebview2/crashpad 进程族可能残留
                //    并继续持有用户数据目录句柄
                var pumpDeadline = Environment.TickCount + 1500;
                while (Environment.TickCount < pumpDeadline)
                {
                    Application.DoEvents();
                    Thread.Sleep(50);
                }
            }
            catch (Exception)
            {
                // 清理路径尽力而为, 不得向 Notepad++ 抛出异常
            }
            finally
            {
                environment = null;
                initTask = null;
            }
        }

        public void Initialize(int zoomLevel, Action<string> openLocalFileInNppAction)
        {
            this.openLocalFileInNppAction = openLocalFileInNppAction;
            var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), CONFIG_FOLDER_NAME, "webview2");
            //var props = new Microsoft.Web.WebView2.WinForms.CoreWebView2CreationProperties();
            //props.UserDataFolder = cacheDir;
            //props.AdditionalBrowserArguments = "--disable-web-security --allow-file-access-from-files --allow-file-access";
            webView = new Microsoft.Web.WebView2.WinForms.WebView2();
            webView.CoreWebView2InitializationCompleted += WebView_CoreWebView2InitializationCompleted;
            var opt = new CoreWebView2EnvironmentOptions();

            var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            CoreWebView2Environment.CreateAsync(null, cacheDir, opt)
                    .ContinueWith(envTask =>
                    {
                        if (envTask.IsFaulted)
                        {
                            return;
                        }

                        environment = envTask.Result;
                        initTask = webView.EnsureCoreWebView2Async(environment);
                        initTask
                            .ContinueWith(ensureTask =>
                            {
                                if (ensureTask.IsFaulted)
                                {
                                    return;
                                }

                                webView.AccessibleName = "webView";
                                webView.Name = "webView";
                                webView.ZoomFactor = ConvertToZoomFactor(zoomLevel);
                                webView.Source = new Uri("about:blank", UriKind.Absolute);
                                webView.Location = new Point(1, 27);
                                webView.Size = new Size(800, 424);
                                webView.Dock = DockStyle.Fill;
                                webView.TabIndex = 0;
                                webView.NavigationStarting += OnWebBrowser_NavigationStarting;
                                webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;
                                webView.NavigationCompleted += WebView_NavigationCompleted;
                                webView.ZoomFactor = ConvertToZoomFactor(zoomLevel);
                            }, scheduler);
                    }, scheduler);
        }

        private void WebView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                webViewInitialized = true;
                if (AfterInitCompletedAction != null) AfterInitCompletedAction();
            }
            else
            {
                MessageBox.Show("WebView2 Initialization Error: " + e?.InitializationException?.Message, "WebView2 Initialization Error");
            }

        }

        public void AddToHost(Control host)
        {
            host.Controls.Add(webView);
        }

        private void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!IsInitialized()) return;

            if (!String.IsNullOrEmpty(currentDocumentPath) && scrollYForFilename.ContainsKey(currentDocumentPath) && scrollYForFilename[currentDocumentPath] > 0)
            {
                ExecuteWebviewAction(new Action(async () =>
                {
                    await webView.ExecuteScriptAsync("window.scrollBy(0, " + scrollYForFilename[currentDocumentPath] + " )");
                    if (RenderingDoneAction != null) RenderingDoneAction();
                }));
            }

            if (e.IsSuccess)
            {
                ExecuteWebviewAction(new Action(async () =>
                {
                    // inject JS to listen to the "Scrollend" event
                    string jsScript = @"

                                window.addEventListener('scrollend', function() {
                                    window.chrome.webview.postMessage('scrollEndUpdate;' + window.scrollY);
                                    var blocks = document.querySelectorAll('.markdown-body [data-line]');
                                    var found = null;
                                    for (var i = 0; i < blocks.length; i++) {
                                        if (blocks[i].getBoundingClientRect().top <= 140) found = blocks[i];
                                    }
                                    if (found) {
                                        var ln = parseInt(found.getAttribute('data-line'), 10);
                                        if (!isNaN(ln)) {
                                            window.chrome.webview.postMessage('previewScroll;' + ln);
                                        }
                                    }
                                });
                            ";
                    await webView.ExecuteScriptAsync(jsScript);
                }));

                ExecuteWebviewAction(new Action(async () =>
                {
                    await webView.ExecuteScriptAsync(checkboxToggleScript);
                }));

                ExecuteWebviewAction(new Action(async () =>
                {
                    await webView.ExecuteScriptAsync(radioToggleScript);
                }));

                blockScrollUpdates = false;
            }

        }

        public Bitmap MakeScreenshot()
        {
            if (!IsInitialized()) return null;
            return null;
        }

        public void PrepareContentUpdate(bool preserveVerticalScrollPosition)
        {
            if (!IsInitialized()) return;
        }

        // Null guard + fallback: when the caret sits on line 2..n of a
        // multi-line block there is no element with that exact data-line —
        // fall back to the last block whose data-line <= target so the
        // preview still scrolls to the enclosing block instead of silently
        // doing nothing (querySelector returns null → JS error → no scroll).
        const string scrollScript =
            "var element = document.querySelector('[data-line=\"{0}\"]');\n" +
            "if (!element) {{\n" +
            "    var target = {0};\n" +
            "    var candidates = document.querySelectorAll('[data-line]');\n" +
            "    for (var i = 0; i < candidates.length; i++) {{\n" +
            "        var ln = parseInt(candidates[i].getAttribute('data-line'), 10);\n" +
            "        if (!isNaN(ln) && ln <= target) {{ element = candidates[i]; }}\n" +
            "    }}\n" +
            "}}\n" +
            "if (element) {{\n" +
            "var headerOffset = 10;\n" +
            "var elementPosition = element.getBoundingClientRect().top;\n" +
            "var offsetPosition = elementPosition + window.pageYOffset - headerOffset;\n" +
            "window.scrollTo({{top: offsetPosition}});\n" +
            "}}";

        const string checkboxToggleScript = @"
            var checkboxes = document.querySelectorAll('input[type=""checkbox""]');
            for (var i = 0; i < checkboxes.length; i++) {
                checkboxes[i].disabled = false;
            }
            if (!window.__checkboxToggleHandlerInstalled) {
                window.__checkboxToggleHandlerInstalled = true;
                document.addEventListener('click', function(e) {
                    if (e.target.tagName === 'INPUT' && e.target.type === 'checkbox') {
                        e.preventDefault();
                        var line = e.target.closest('[data-line]');
                        if (line) {
                            window.chrome.webview.postMessage('checkboxToggle;' + line.getAttribute('data-line'));
                        }
                    }
                }, true);
            }";

        const string radioToggleScript = @"
            var lis = document.querySelectorAll('li[data-line]');
            for (var i = 0; i < lis.length; i++) {
                var li = lis[i];
                var text = li.innerHTML;
                if (/^\([ xX]\)\s/.test(text)) {
                    li.innerHTML = text.replace(/^\([ xX]\)/, '<span class=""md-radio"">$&</span>');
                }
            }
            if (!window.__radioToggleHandlerInstalled) {
                window.__radioToggleHandlerInstalled = true;
                document.addEventListener('click', function(e) {
                    if (e.target.classList && e.target.classList.contains('md-radio')) {
                        e.preventDefault();
                        var line = e.target.closest('[data-line]');
                        if (line) {
                            var parentList = e.target.closest('ul, ol');
                            if (parentList) {
                                var radios = parentList.querySelectorAll('.md-radio');
                                for (var j = 0; j < radios.length; j++) {
                                    radios[j].textContent = '( )';
                                }
                            }
                            e.target.textContent = '(x)';
                            window.chrome.webview.postMessage('radioToggle;' + line.getAttribute('data-line'));
                        }
                    }
                }, true);
            }";


        public void ScrollToElementWithLineNo(int lineNo)
        {
            if (!IsInitialized()) return;
            if (lineNo <= 0) lineNo = 0;
            ExecuteWebviewAction(new Action(async () =>
            {
                await webView.ExecuteScriptAsync(string.Format(scrollScript, lineNo));
            }));
        }

        public void SetContent(string content, string body, string style, string currentDocumentPath)
        {
            if (!IsInitialized()) return;

            var currentPath = Path.GetDirectoryName(currentDocumentPath);
            var replaceFileMapping = "file:///" + currentPath.Replace('\\', '/');

            content = content.Replace(replaceFileMapping, virtualHostProtocol + virtualHostName);
            body = body.Replace(replaceFileMapping, virtualHostProtocol + virtualHostName);

            var fullReload = false;
            if (forceFullReload)
            {
                forceFullReload = false;
                fullReload = true;
            }
            if (this.currentDocumentPath != currentDocumentPath)
            {
                ExecuteWebviewAction(new Action(() =>
                {
                    webView.CoreWebView2.SetVirtualHostNameToFolderMapping(virtualHostName, currentPath, CoreWebView2HostResourceAccessKind.Allow);
                }));
                this.currentDocumentPath = currentDocumentPath;
                fullReload = true;
            }

            // Detect the actual outline DOM element, not the string "outline-sidebar":
            // the outline CSS rules (.outline-sidebar {...}) are embedded in every page via
            // the style placeholder, so a bare substring check is always true and the toggle
            // would never force a full reload.
            var pageHasOutline = content.Contains("<nav id=\"outline-sidebar\"");
            if (!fullReload && this.currentPageHasOutline != pageHasOutline)
            {
                fullReload = true;
            }
            this.currentPageHasOutline = pageHasOutline;

            if (!fullReload && currentBody != null && currentStyle != null)
            {
                if (currentBody != body)
                {
                    currentBody = body;
                    ExecuteWebviewAction(new Action(async () =>
                    {
                        await webView.ExecuteScriptAsync(
                            "(function(){var om=document.getElementById('outline-main');if(om){om.innerHTML='" + HttpUtility.JavaScriptStringEncode(currentBody) + "';if(window.buildOutline)window.buildOutline();}else{document.body.innerHTML='" + HttpUtility.JavaScriptStringEncode(currentBody) + "';}})();" +
                            // comrak emits ```mermaid fences as
                            // <pre><code class="language-mermaid"> — mermaid.run()
                            // only picks up .mermaid elements, so convert the
                            // fresh blocks before running it (offline/CDN
                            // failure keeps the styled code block as-is).
                            "if(typeof mermaid!=='undefined'){document.querySelectorAll('pre > code.language-mermaid').forEach(function(el){var h=document.createElement('div');h.className='mermaid';var p=el.closest('pre');var ln=p?p.getAttribute('data-line'):null;if(ln)h.setAttribute('data-line',ln);h.textContent=el.textContent;if(p){p.replaceWith(h);}else{el.replaceWith(h);}});mermaid.run();}"
                        );
                        await webView.ExecuteScriptAsync(checkboxToggleScript);
                        await webView.ExecuteScriptAsync(radioToggleScript);
                    }));
                }
                if (currentStyle != style)
                {
                    currentStyle = style;
                    ExecuteWebviewAction(new Action(async () =>
                    {
                        // Replace the style element by its fixed id. The old
                        // lastElementChild removal relied on DOM ordering
                        // (it actually deleted the mermaid init script) and
                        // accumulated one extra <style> per theme switch.
                        await webView.ExecuteScriptAsync(
                            "var style = document.getElementById('md-preview-style');\n" +
                            "if (!style) { style = document.createElement('style'); style.id = 'md-preview-style'; style.type = 'text/css'; document.head.appendChild(style); }\n" +
                            "style.textContent = '" + HttpUtility.JavaScriptStringEncode(currentStyle) + "'; \n"
                            );
                    }));
                }
            }
            else
            {
                currentBody = body;
                currentStyle = style;
                ExecuteWebviewAction(new Action(() =>
                {
                    if (content.Length > NavigateToStringMaxChars)
                        NavigateLargeContentCore(content);
                    else
                        webView.NavigateToString(content);
                }));
            }
        }

        /// <summary>
        /// Deliver an oversized page: write it to the temp folder and navigate
        /// to it via the dedicated virtual host. MUST run on the WebView
        /// thread (inside ExecuteWebviewAction).
        /// </summary>
        private void NavigateLargeContentCore(string content)
        {
            if (tmpHtmlDir == null)
                tmpHtmlDir = Path.Combine(Path.GetTempPath(), "NppMarkdownPanel");
            Directory.CreateDirectory(tmpHtmlDir);

            var fileName = "preview_" + Interlocked.Increment(ref tmpHtmlFileCounter) + ".html";
            File.WriteAllText(Path.Combine(tmpHtmlDir, fileName), content);

            // Prune stale preview files (best-effort: locked files are skipped
            // and retried on the next oversized render).
            try
            {
                foreach (var stale in Directory.GetFiles(tmpHtmlDir, "preview_*.html"))
                {
                    if (Path.GetFileName(stale) != fileName)
                    {
                        try { File.Delete(stale); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    }
                }
            }
            catch (IOException) { }

            webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                tmpVirtualHostName, tmpHtmlDir, CoreWebView2HostResourceAccessKind.Allow);
            webView.CoreWebView2.Navigate("http://" + tmpVirtualHostName + "/" + fileName);
        }

        public void SetZoomLevel(int zoomLevel)
        {
            if (!IsInitialized()) return;
            double zoomFactor = ConvertToZoomFactor(zoomLevel);
            ExecuteWebviewAction(new Action(() =>
            {
                if (webView.ZoomFactor != zoomFactor)
                    webView.ZoomFactor = zoomFactor;

            }));
        }

        private double ConvertToZoomFactor(int zoomLevel)
        {
            double zoomFactor = Convert.ToDouble(zoomLevel) / 100;
            return zoomFactor;
        }

        void OnWebBrowser_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            var navUri = e.Uri.ToString();

            // Same-document anchor navigations on our own pages (in-page
            // #links). Only our own delivery origins qualify here — an
            // external URL that happens to carry a '#fragment' must still
            // take the external branch below.
            if (navUri.StartsWith("about:blank#")
                || (navUri.StartsWith("data:") && navUri.Contains("#"))
                || (navUri.StartsWith("http://" + tmpVirtualHostName + "/") && navUri.Contains("#")))
                return;

            if (navUri.StartsWith("about:blank"))
            {
                e.Cancel = true;
            }
            else if (navUri.StartsWith("data:"))
            {
                // NavigateToString delivery — allow.
            }
            else if (navUri.StartsWith(virtualHostProtocol + virtualHostName))
            {
                // Link to a file next to the document: open it in Notepad++.
                e.Cancel = true;
                var currentPath = Path.GetDirectoryName(currentDocumentPath);
                var localUri = navUri.Replace(virtualHostProtocol + virtualHostName, currentPath);
                var fragmentPos = localUri.IndexOf('#');
                if (fragmentPos >= 0) localUri = localUri.Substring(0, fragmentPos);
                localUri = Uri.UnescapeDataString(localUri);
                openLocalFileInNppAction(localUri);
            }
            else if (navUri.StartsWith("http://" + tmpVirtualHostName + "/"))
            {
                // Internal oversized-preview navigation: neither cancel
                // (it is our own delivery path) nor force a full reload.
            }
            else
            {
                // External link: never navigate the preview in-panel — the
                // user would be stuck with a hijacked view (no address bar,
                // no back button) and the postMessage bridge would be
                // exposed to the foreign page's JS context. Cancel and hand
                // it to the system browser instead.
                e.Cancel = true;
                forceFullReload = true;
                try
                {
                    using (System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(navUri) { UseShellExecute = true })) { }
                }
                catch (Exception) { }
            }
        }

        public string GetRenderingEngineName()
        {
            return "EDGE";
        }


        private void ExecuteWebviewAction(Action action)
        {
            try
            {
                if (webView != null)
                    webView.Invoke(action);
            }
            catch (Exception ex)
            {
            }
        }

        public bool IsInitialized()
        {
            return webViewInitialized && webView != null;
        }

        Dictionary<string, int> scrollYForFilename = new Dictionary<string, int>();
        bool blockScrollUpdates = true;

        private void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string message = e.TryGetWebMessageAsString();

            var splittedParams = message.Split(';');
            if (splittedParams.Length > 1)
            {
                string action = splittedParams[0];
                if (action == "scrollEndUpdate" && !blockScrollUpdates)
                {
                    try
                    {
                        var scrolly = int.Parse(splittedParams[1].Split('.')[0]);
                        if (scrollYForFilename.ContainsKey(currentDocumentPath))
                        {
                            scrollYForFilename[currentDocumentPath] = scrolly;
                        }
                        else
                        {
                            scrollYForFilename.Add(currentDocumentPath, scrolly);
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                }
                else if (action == "checkboxToggle")
                {
                    try
                    {
                        int lineNo = int.Parse(splittedParams[1]);
                        if (CheckboxToggleAction != null)
                        {
                            CheckboxToggleAction(lineNo);
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                }
                else if (action == "radioToggle")
                {
                    try
                    {
                        int lineNo = int.Parse(splittedParams[1]);
                        if (RadioToggleAction != null)
                        {
                            RadioToggleAction(lineNo);
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                }
                else if (action == "previewScroll" && !blockScrollUpdates)
                {
                    // Bidirectional sync: preview reached a new top block.
                    // blockScrollUpdates also gates this direction (document
                    // switches / full reloads must not drive the editor).
                    try
                    {
                        int lineNo = int.Parse(splittedParams[1]);
                        if (PreviewScrollAction != null)
                        {
                            PreviewScrollAction(lineNo);
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                }
            }
        }

        public void CurrentDocumentRenamed(string newDocumentPath)
        {
            if (scrollYForFilename.ContainsKey(currentDocumentPath))
            {
                scrollYForFilename.Add(newDocumentPath, scrollYForFilename[currentDocumentPath]);
                scrollYForFilename.Remove(currentDocumentPath);
            }

            currentDocumentPath = newDocumentPath;
        }

        public void StopScrollPositionTracking()
        {
            blockScrollUpdates = true;
        }

        public void ExportToPdf(string filePath)
        {
            if (!IsInitialized()) return;
            ExecuteWebviewAction(new Action(async () =>
            {
                await webView.CoreWebView2.PrintToPdfAsync(filePath);
            }));
        }

    }
}
