using NppMarkdownPanel.Entities;
using NppMarkdownPanel.Generator;
using NppMarkdownPanel.Webbrowser;
using PanelCommon;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using TheArtOfDev.HtmlRenderer.WinForms.Utilities;
using Webview2Viewer;

namespace NppMarkdownPanel.Forms
{
    public partial class MarkdownPreviewForm : DockingFormBase, IViewerInterface
    {
        const string DEFAULT_HTML_BASE =
         @"<!DOCTYPE html>
            <html>
                <head>                    
                    <meta http-equiv=""X-UA-Compatible"" content=""IE=edge""></meta>
                    <meta http-equiv=""content-type"" content=""text/html; charset=utf-8""></meta>
                    <title>{0}</title>
                    <style type=""text/css"">
                    {1}
                    </style>
                    <script src=""https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.min.js"" onerror=""this.remove();""></script>
                    <script>
                    if(typeof mermaid!=='undefined'){{mermaid.initialize({{ startOnLoad: false }});}}
                    </script>
                </head>
                <body class=""markdown-body"" style=""{2}"">
                {3}
                <script>
                if(typeof mermaid!=='undefined'){{mermaid.run();}}
                </script>
                </body>
            </html>
            ";

        const string OUTLINE_HTML_BASE =
         @"<!DOCTYPE html>
            <html>
                <head>                    
                    <meta http-equiv=""X-UA-Compatible"" content=""IE=edge""></meta>
                    <meta http-equiv=""content-type"" content=""text/html; charset=utf-8""></meta>
                    <title>{0}</title>
                    <style type=""text/css"">
                    {1}
                    </style>
                    <script src=""https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.min.js"" onerror=""this.remove();""></script>
                    <script>
                    if(typeof mermaid!=='undefined'){{mermaid.initialize({{ startOnLoad: false }});}}
                    </script>
                </head>
                <body class=""outline-enabled"" style=""{2}"">
                    <nav id=""outline-sidebar"" class=""outline-sidebar"">
                        <div class=""outline-header"">Outline</div>
                        <div id=""outline-content"" class=""outline-content""></div>
                    </nav>
                    <div id=""outline-main"" class=""outline-main markdown-body"">{3}</div>
                    <button id=""outline-toggle"" class=""outline-toggle"" title=""Toggle Outline"" onclick=""document.getElementById('outline-sidebar').classList.toggle('collapsed');this.classList.toggle('collapsed');"">&#9776;</button>
OUTLINE_SCRIPT_PLACEHOLDER
                    <script>
                    if(typeof mermaid!=='undefined'){{mermaid.run();}}
                    </script>
                </body>
            </html>
            ";

        const string OUTLINE_SCRIPT = @"<script>
(function(){
    var content = document.getElementById('outline-content');
    var main = document.getElementById('outline-main');
    var sidebar = document.getElementById('outline-sidebar');
    var toggle = document.getElementById('outline-toggle');

    window.buildOutline = function() {
        content.innerHTML = '';
        var hs = main.querySelectorAll('h1, h2, h3, h4, h5, h6');
        if (hs.length === 0) {
            sidebar.style.display = 'none';
            toggle.style.display = 'none';
            return;
        }
        sidebar.style.display = '';
        toggle.style.display = '';
        var min = 7;
        for (var i = 0; i < hs.length; i++) {
            var lvl = parseInt(hs[i].tagName.substring(1));
            if (lvl < min) min = lvl;
        }
        for (var i = 0; i < hs.length; i++) {
            var h = hs[i];
            var lvl = parseInt(h.tagName.substring(1)) - min + 1;
            var txt = h.textContent || h.innerText || '';
            var ln = h.getAttribute('data-line') || '';
            var a = document.createElement('a');
            a.className = 'outline-item outline-l' + lvl;
            a.setAttribute('data-line', ln);
            a.textContent = txt;
            a.addEventListener('click', function(e) {
                e.preventDefault();
                var t = main.querySelector('[data-line=""' + this.getAttribute('data-line') + '""]');
                if (t) t.scrollIntoView({behavior:'smooth',block:'start'});
            });
            content.appendChild(a);
        }
    };

    window.addEventListener('scroll', function() {
        var items = content.querySelectorAll('.outline-item');
        if (items.length === 0) return;
        var hs = main.querySelectorAll('h1, h2, h3, h4, h5, h6');
        var sp = window.scrollY + 140;
        var fl = null;
        for (var i = 0; i < hs.length; i++) {
            if (hs[i].getBoundingClientRect().top + window.scrollY <= sp) fl = hs[i].getAttribute('data-line');
        }
        for (var i = 0; i < items.length; i++) items[i].classList.remove('active');
        if (fl) {
            var m = content.querySelector('[data-line=""' + fl + '""]');
            if (m) m.classList.add('active');
        }
    });

    buildOutline();
})();
</script>";

        const string MSG_NO_SUPPORTED_FILE_EXT = "<h3>The current file <u>{0}</u> has no valid Markdown file extension.</h3><div>Valid file extensions:{1}</div>";

        private Task<RenderResult> renderTask;
        private int renderGeneration;

        private string htmlContentForExport;

        // 渲染缓存 (LRU, 容量 4): 切换回未修改的文档时直接命中, 不再全量重渲染。
        private const int RenderCacheCapacity = 4;
        private readonly LinkedList<RenderCacheEntry> renderCacheLru = new LinkedList<RenderCacheEntry>();
        private readonly Dictionary<RenderCacheKey, LinkedListNode<RenderCacheEntry>> renderCacheMap = new Dictionary<RenderCacheKey, LinkedListNode<RenderCacheEntry>>();
        private string currentMarkdownText;
        private Settings settings;
        private string currentFilePath;
        private IWebbrowserControl webbrowserControl;
        private IWebbrowserControl webview1Instance;
        private IWebbrowserControl webview2Instance;
        private bool cleanupStarted;
        private Action<int> checkboxToggleHandler;
        private Action<int> radioToggleHandler;

        public void SetCheckboxToggleHandler(Action<int> handler)
        {
            checkboxToggleHandler = handler;
            if (webbrowserControl != null)
            {
                webbrowserControl.CheckboxToggleAction = handler;
            }
        }

        public void SetRadioToggleHandler(Action<int> handler)
        {
            radioToggleHandler = handler;
            if (webbrowserControl != null)
            {
                webbrowserControl.RadioToggleAction = handler;
            }
        }

        public void UpdateSettings(Settings newSettings, Action<string> openLocalFileInNppAction)
        {
            this.settings = newSettings;

            var isDarkModeEnabled = newSettings.IsDarkModeEnabled;
            if (isDarkModeEnabled)
            {
                tbPreview.BackColor = Color.Black;

                foreach (ToolStripItem tsItem in tbPreview.Items)
                {
                    tsItem.ForeColor = Color.White;
                }

                //btnSaveWithLightTheme.ForeColor = Color.White;

                // Footer
                toolStripStatusLabel1.ForeColor = Color.White;
                footerStatusStrip.BackColor = Color.Black;
            }
            else
            {
                tbPreview.BackColor = SystemColors.Control;

                foreach (ToolStripItem tsItem in tbPreview.Items)
                {
                    tsItem.ForeColor = SystemColors.ControlText;
                }

                //btnSaveWithLightTheme.ForeColor = SystemColors.ControlText;

                // Footer
                footerStatusStrip.BackColor = SystemColors.Control;
                toolStripStatusLabel1.ForeColor = SystemColors.ControlText;
            }

            tbPreview.Visible = newSettings.ShowToolbar;
            footerStatusStrip.Visible = newSettings.ShowStatusbar;

            if (webbrowserControl != null && webbrowserControl.GetRenderingEngineName() != settings.RenderingEngine)
            {
                InitRenderingEngine(settings, openLocalFileInNppAction);
            }

        }

        private MarkdownService markdownService;
        private ActionRef<Message> wndProcCallback;

        public static IViewerInterface InitViewer(Settings settings, ActionRef<Message> wndProcCallback)
        {
            return new MarkdownPreviewForm(settings, wndProcCallback);
        }

        private MarkdownPreviewForm(Settings settings, ActionRef<Message> wndProcCallback)
        {
            InitializeComponent();

            this.wndProcCallback = wndProcCallback;
            // v4.0 fork: Rust 共享核心优先，rustrender.dll 缺失时自动回落 Markdig。
            markdownService = new MarkdownService(RustRenderWrapper.RustRenderService.CreateGenerator());
            markdownService.PreProcessorCommandFilename = settings.PreProcessorCommandFilename;
            markdownService.PreProcessorArguments = settings.PreProcessorArguments;
            markdownService.PostProcessorCommandFilename = settings.PostProcessorCommandFilename;
            markdownService.PostProcessorArguments = settings.PostProcessorArguments;
            this.settings = settings;
            panel1.Visible = true;

            //InitRenderingEngine(settings);
        }

        public void InitRenderingEngine(Settings newSettings, Action<string> openLocalFileInNppAction)
        {
            panel1.Controls.Clear();

            if (newSettings.IsRenderingEngineIE11())
            {
                if (webview1Instance == null)
                {
                    webbrowserControl = new IE11WebbrowserControl();
                    webbrowserControl.Initialize(newSettings.ZoomLevel, openLocalFileInNppAction);
                    webview1Instance = webbrowserControl;
                }
                else
                {
                    webbrowserControl = webview1Instance;
                }
            }
            else if (newSettings.IsRenderingEngineEdge())
            {
                if (webview2Instance == null)
                {
                    webbrowserControl = new Webview2WebbrowserControl();
                    webbrowserControl.Initialize(newSettings.ZoomLevel, openLocalFileInNppAction);
                    webview2Instance = webbrowserControl;
                }
                else
                {
                    webbrowserControl = webview2Instance;
                }
            }

            webbrowserControl.AddToHost(panel1);
            webbrowserControl.RenderingDoneAction = () => { HideScreenshotAndShowBrowser(); };
            webbrowserControl.StatusTextChangedAction = (status) => { toolStripStatusLabel1.Text = status; };
            webbrowserControl.CheckboxToggleAction = checkboxToggleHandler;
            webbrowserControl.RadioToggleAction = radioToggleHandler;
        }

        private RenderResult RenderHtmlInternal(string currentText, string filepath)
        {
            // v4.0 fork: 将暗色模式 + 预览主题同步给 Rust 渲染核心（syntect
            // 高亮主题随渲染切换）。主题 class 走 FFI bits 7-9（0 = 旧行为）。
            // flags + class 经 SetOptions 单锁原子写入（消除两次赋值间的撕裂
            // 窗口）；预览快照字同时作为渲染缓存 key 的一部分。
            var theme = ThemeCatalog.Find(settings.PreviewTheme);
            var darkTheme = settings.IsDarkBoard();
            var flags = (RustRenderWrapper.RustRenderService.CurrentFlags & ~RustRenderWrapper.RenderFlags.DarkMode)
                | (darkTheme ? RustRenderWrapper.RenderFlags.DarkMode : 0);
            var themeClass = ThemeCatalog.HighlightClass(theme, darkTheme);
            RustRenderWrapper.RustRenderService.SetOptions(flags, themeClass);
            var previewOptions = RustRenderWrapper.RustRenderService.BuildOptionsWord(flags, themeClass);

            var defaultBodyStyle = "";
            var markdownStyleContent = GetCssContent();
            var htmlTemplate = settings.ShowOutline ? OUTLINE_HTML_BASE : DEFAULT_HTML_BASE;

            if (!IsValidFileExtension(currentFilePath))
            {
                var invalidExtensionMessageBody = string.Format(MSG_NO_SUPPORTED_FILE_EXT, Path.GetFileName(filepath), settings.SupportedFileExt);
                var invalidExtensionMessage = string.Format(htmlTemplate, Path.GetFileName(filepath), markdownStyleContent, defaultBodyStyle, invalidExtensionMessageBody);
                if (settings.ShowOutline)
                    invalidExtensionMessage = InjectOutlineScript(invalidExtensionMessage);

                return new RenderResult(invalidExtensionMessage, invalidExtensionMessage, invalidExtensionMessageBody, markdownStyleContent);
            }

            // 渲染缓存: 同一 (文档, 文本, 样式, 大纲, 导出策略, options) 组合直接
            // 复用上次结果。options 入 key 防止撕裂/错配渲染被钉在缓存里。
            var exportNeeded = !string.IsNullOrWhiteSpace(settings.HtmlFileName);
            var cacheKey = new RenderCacheKey(filepath, currentText, markdownStyleContent, settings.ShowOutline, exportNeeded, previewOptions);
            var cached = TryGetCachedRender(cacheKey);
            if (cached != null)
                return cached;

            var resultForBrowser = markdownService.ConvertToHtml(currentText, filepath, true);
            // 导出版惰性渲染: 仅在配置了 HtmlFileName 自动落盘时才随预览一起计算,
            // 保存/复制路径按需生成 —— 每次渲染成本减半。
            var resultForExport = exportNeeded ? markdownService.ConvertToHtml(currentText, null, false) : null;

            var markdownHtmlBrowser = string.Format(htmlTemplate, Path.GetFileName(filepath), markdownStyleContent, defaultBodyStyle, resultForBrowser);
            var markdownHtmlFileExport = exportNeeded ? string.Format(htmlTemplate, Path.GetFileName(filepath), markdownStyleContent, defaultBodyStyle, resultForExport) : null;

            if (settings.ShowOutline)
            {
                markdownHtmlBrowser = InjectOutlineScript(markdownHtmlBrowser);
                if (exportNeeded)
                    markdownHtmlFileExport = InjectOutlineScript(markdownHtmlFileExport);
            }

            var renderResult = new RenderResult(markdownHtmlBrowser, markdownHtmlFileExport, resultForBrowser, markdownStyleContent);
            StoreCachedRender(cacheKey, renderResult);
            return renderResult;
        }

        private string GetCssContent(bool forceLightTheme = false)
        {
            // Path of plugin directory
            var cssContent = "";

            var assemblyPath = Path.GetDirectoryName(Assembly.GetAssembly(GetType()).Location);

            // v0.9.3+: 显式选择的预览主题（非 Default）走语义 token 样式表，
            // ThemeCatalog 注入 :root 变量块；主题部署缺失时回退旧样式。
            var useDarkTheme = settings.IsDarkBoard() && !forceLightTheme;
            var theme = ThemeCatalog.Find(settings.PreviewTheme);
            if (!theme.IsDefault)
            {
                var themedCss = ThemeCatalog.GenerateCss(theme, useDarkTheme);
                if (themedCss != null)
                {
                    return themedCss;
                }
                // fall through to the legacy sheet below
            }

            var defaultCss = useDarkTheme ? Settings.DefaultDarkModeCssFile : Settings.DefaultCssFile;
            var customCssFile = useDarkTheme ? settings.CssDarkModeFileName : settings.CssFileName;
            if (File.Exists(customCssFile))
            {
                cssContent = File.ReadAllText(customCssFile);
            }
            else
            {
                cssContent = File.ReadAllText(assemblyPath + "\\" + defaultCss);
            }

            return cssContent;
        }

        public void RenderMarkdown(string currentText, string filepath, bool preserveVerticalScrollPosition = true)
        {
            if (webbrowserControl == null) return;

            Action renderAction = () =>
            {
                int myGeneration = ++renderGeneration;
                MakeAndDisplayScreenShot();
                webbrowserControl.PrepareContentUpdate(preserveVerticalScrollPosition);

                var context = TaskScheduler.FromCurrentSynchronizationContext();
                renderTask = new Task<RenderResult>(() => RenderHtmlInternal(currentText, filepath));
                renderTask.ContinueWith((renderedText) =>
                {
                    if (!cleanupStarted && myGeneration == renderGeneration)
                    {
                        webbrowserControl.SetContent(renderedText.Result.ResultForBrowser, renderedText.Result.ResultBody, renderedText.Result.ResultStyle, currentFilePath);
                        htmlContentForExport = renderedText.Result.ResultForExport;
                        currentMarkdownText = currentText;
                        if (!String.IsNullOrWhiteSpace(settings.HtmlFileName))
                        {
                            bool valid = Utils.ValidateFileSelection(settings.HtmlFileName, out string fullPath, out string error, "HTML Output");
                            if (valid)
                            {
                                settings.HtmlFileName = fullPath;
                                writeHtmlContentToFile(settings.HtmlFileName);
                            }
                        }
                        webbrowserControl.SetZoomLevel(settings.ZoomLevel);
                    }

                }, context);
                renderTask.Start();
            };
            webbrowserControl.AfterInitCompletedAction = renderAction;
            if (webbrowserControl.IsInitialized())
            {
                webbrowserControl.AfterInitCompletedAction = null;
                renderAction();
            }
        }

        /// <summary>
        /// Makes and displays a screenshot of the current browser content to prevent it from flickering 
        /// while loading updated content
        /// </summary>
        private void MakeAndDisplayScreenShot()
        {
            if (webbrowserControl == null) return;

            Bitmap bm = webbrowserControl.MakeScreenshot();
            if (bm != null)
            {
                pictureBoxScreenshot.Image = bm;
                pictureBoxScreenshot.Visible = true;
            }

        }

        private void HideScreenshotAndShowBrowser()
        {
            if (webbrowserControl == null) return;

            if (pictureBoxScreenshot.Image != null)
            {
                pictureBoxScreenshot.Visible = false;
                pictureBoxScreenshot.Image = null;
            }
        }

        public void ScrollToElementWithLineNo(int lineNo)
        {
            if (webbrowserControl == null) return;

            webbrowserControl.ScrollToElementWithLineNo((int)lineNo);
        }

        protected override void WndProc(ref Message m)
        {
            wndProcCallback(ref m);

            //Continue the processing, as we only toggle
            base.WndProc(ref m);
        }

        private void btnSaveHtml_Click(object sender, EventArgs e)
        {
            ShowSaveAs(false);
        }


        private void btnSaveLightTheme_Click(object sender, EventArgs e)
        {
            ShowSaveAs(true);
        }

        private void ShowSaveAs(bool overrideLightTheme)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "html files (*.html, *.htm)|*.html;*.htm|All files (*.*)|*.*";
                saveFileDialog.RestoreDirectory = true;
                saveFileDialog.InitialDirectory = Path.GetDirectoryName(currentFilePath);
                saveFileDialog.FileName = Path.GetFileNameWithoutExtension(currentFilePath);
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    writeHtmlContentToFile(saveFileDialog.FileName, overrideLightTheme);
                }
            }
        }

        private void writeHtmlContentToFile(string filename, bool overrideLightTheme = false)
        {
            if (!string.IsNullOrEmpty(filename))
            {
                // 导出版一律按需生成：暗色版通常已随预览算好（配置了自动落盘时），
                // 亮色版必须以亮色 options 单独渲染（不能复用暗色 body）。
                if (overrideLightTheme)
                    File.WriteAllText(filename, RenderExportHtml(true));
                else
                    File.WriteAllText(filename, htmlContentForExport ?? RenderExportHtml(false));
            }
        }

        public bool IsValidFileExtension(string filename)
        {
            if (settings.AllowAllExtensions) return true;
            var currentExtension = Path.GetExtension(filename).ToLower();
            var matchExtensionList = false;
            try
            {
                matchExtensionList = settings.SupportedFileExt.Split(',').Any(ext => ext != null && currentExtension.Equals("." + ext.Trim().ToLower()));
                if (currentExtension == "" && settings.SupportFilesWithNoExt) matchExtensionList = true;
            }
            catch (Exception)
            {
            }

            return matchExtensionList;
        }

        public void SetMarkdownFilePath(string filepath, bool isRename = false)
        {
            if (webbrowserControl == null) return;

            if (isRename)
            {
                webbrowserControl.CurrentDocumentRenamed(filepath);
            }
            else
            {
                if (currentFilePath != filepath)
                {
                    // We're about to switch to a new file. Stop tracking the current scolly value, as we can get unexpected results now...
                    webbrowserControl.StopScrollPositionTracking();
                }
            }

            currentFilePath = filepath;

        }

        public void Cleanup()
        {
            cleanupStarted = true;
            if (renderTask != null)
            {
                try
                {
                    // 有界等待: NPPN_SHUTDOWN 期间大文件渲染不允许无限阻塞宿主退出
                    // (宿主被拖死 = plugins\ 目录无法替换的直接原因)
                    renderTask.Wait(TimeSpan.FromSeconds(3));
                }
                catch (AggregateException)
                {
                    // 渲染任务失败/取消, 直接继续清理
                }
                renderTask = null;
            }
            ClearRenderCache();
            if (webview2Instance != null)
            {
                webview2Instance.Dispose();
                webview2Instance = null;
            }
            if (webview1Instance != null)
            {
                webview1Instance.Dispose();
                webview1Instance = null;
            }
            webbrowserControl = null;
        }

        private static string InjectOutlineScript(string html)
        {
            return html.Replace("OUTLINE_SCRIPT_PLACEHOLDER", OUTLINE_SCRIPT);
        }

        /// <summary>
        /// 惰性导出渲染: 保存/复制时才生成导出版 HTML。
        /// 日常预览渲染不再为导出版支付每键一份的完整渲染成本。
        /// </summary>
        private string RenderExportHtml(bool forceLightTheme)
        {
            var htmlTemplate = settings.ShowOutline ? OUTLINE_HTML_BASE : DEFAULT_HTML_BASE;
            // 亮色导出必须以亮色板 class + 清零 DarkMode 位的 options 重新渲染
            // body —— 复用预览（暗色）快照会把暗色代码块烘进亮色 HTML。
            // options 经参数显式传入，不切换共享快照，导出期间不影响预览渲染。
            var theme = ThemeCatalog.Find(settings.PreviewTheme);
            var darkBoard = forceLightTheme ? false : settings.IsDarkBoard();
            var flags = (RustRenderWrapper.RustRenderService.CurrentFlags & ~RustRenderWrapper.RenderFlags.DarkMode)
                | (darkBoard ? RustRenderWrapper.RenderFlags.DarkMode : 0);
            var exportOptions = RustRenderWrapper.RustRenderService.BuildOptionsWord(
                flags, ThemeCatalog.HighlightClass(theme, darkBoard));
            var resultForExport = markdownService.ConvertToHtml(currentMarkdownText, null, false, exportOptions);
            var html = string.Format(htmlTemplate, Path.GetFileName(currentFilePath), GetCssContent(forceLightTheme), "", resultForExport);
            if (settings.ShowOutline)
                html = InjectOutlineScript(html);
            return html;
        }

        #region 渲染缓存

        private struct RenderCacheKey : IEquatable<RenderCacheKey>
        {
            private readonly string filepath;
            private readonly string text;
            private readonly string style;
            private readonly bool outline;
            private readonly bool exportNeeded;
            private readonly uint options;

            public RenderCacheKey(string filepath, string text, string style, bool outline, bool exportNeeded, uint options)
            {
                this.filepath = filepath ?? "";
                this.text = text ?? "";
                this.style = style ?? "";
                this.outline = outline;
                this.exportNeeded = exportNeeded;
                this.options = options;
            }

            public bool Equals(RenderCacheKey other)
            {
                return outline == other.outline
                    && exportNeeded == other.exportNeeded
                    && options == other.options
                    && string.Equals(filepath, other.filepath, StringComparison.Ordinal)
                    && string.Equals(text, other.text, StringComparison.Ordinal)
                    && string.Equals(style, other.style, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is RenderCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var h = 17;
                    h = h * 31 + StringComparer.Ordinal.GetHashCode(filepath);
                    h = h * 31 + StringComparer.Ordinal.GetHashCode(text);
                    h = h * 31 + StringComparer.Ordinal.GetHashCode(style);
                    h = h * 31 + (outline ? 1 : 0);
                    h = h * 31 + (exportNeeded ? 1 : 0);
                    h = h * 31 + options.GetHashCode();
                    return h;
                }
            }
        }

        private sealed class RenderCacheEntry
        {
            public RenderCacheKey Key;
            public RenderResult Result;
        }

        private RenderResult TryGetCachedRender(RenderCacheKey key)
        {
            lock (renderCacheMap)
            {
                if (!renderCacheMap.TryGetValue(key, out var node))
                    return null;
                // 命中即提升到 LRU 头部。
                renderCacheLru.Remove(node);
                renderCacheLru.AddFirst(node);
                return node.Value.Result;
            }
        }

        private void StoreCachedRender(RenderCacheKey key, RenderResult result)
        {
            lock (renderCacheMap)
            {
                if (renderCacheMap.TryGetValue(key, out var existing))
                {
                    renderCacheLru.Remove(existing);
                    renderCacheMap.Remove(key);
                }
                var node = new LinkedListNode<RenderCacheEntry>(new RenderCacheEntry { Key = key, Result = result });
                renderCacheLru.AddFirst(node);
                renderCacheMap[key] = node;
                while (renderCacheLru.Count > RenderCacheCapacity)
                {
                    var last = renderCacheLru.Last;
                    renderCacheLru.RemoveLast();
                    renderCacheMap.Remove(last.Value.Key);
                }
            }
        }

        private void ClearRenderCache()
        {
            lock (renderCacheMap)
            {
                renderCacheMap.Clear();
                renderCacheLru.Clear();
            }
        }

        #endregion

        private void btnCopyToClipboard_Click(object sender, EventArgs e)
        {
            var export = htmlContentForExport ?? RenderExportHtml(false);
            ClipboardHelper.CopyToClipboard(export, export);
        }

        public void ExportToPdf()
        {
            if (webbrowserControl == null) return;

            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "pdf files (*.pdf)|*.pdf";
                saveFileDialog.RestoreDirectory = true;
                saveFileDialog.InitialDirectory = Path.GetDirectoryName(currentFilePath);
                saveFileDialog.FileName = Path.GetFileNameWithoutExtension(currentFilePath);
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    webbrowserControl.ExportToPdf(saveFileDialog.FileName);
                }
            }
        }

        private void btnExportToPdf_Click(object sender, EventArgs e)
        {
            ExportToPdf();
        }
    }
}
