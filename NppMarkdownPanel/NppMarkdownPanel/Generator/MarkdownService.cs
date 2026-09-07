using NppMarkdownPanel.Generator;
using PanelCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NppMarkdownPanel.Generator
{
    public class MarkdownService
    {
        private const string INPUT_FILENAME_PLACEHOLDER = "%inputfile%";
        private const string OUTPUT_FILENAME_PLACEHOLDER = "%outputfile%";

        // 外部预/后处理器等待上限: 挂起的处理器不得无限期阻塞渲染链
        // (渲染 Task → UI continuation → 预览停更)。
        private const int ExternalProcessorTimeoutMs = 30000;

        private IMarkdownGenerator markdownGenerator;

        public string PreProcessorCommandFilename { get; set; }
        public string PreProcessorArguments { get; set; }
        public string PostProcessorCommandFilename { get; set; }
        public string PostProcessorArguments { get; set; }

        public MarkdownService(IMarkdownGenerator markdownGenerator)
        {
            this.markdownGenerator = markdownGenerator;
        }

        public string ConvertToHtml(string markDownText, string filepath, bool supportEscapeCharsInUris)
        {
            var input = executeExternalProcessor(PreProcessorCommandFilename, PreProcessorArguments, markDownText);
            var html = markdownGenerator.ConvertToHtml(input, filepath, supportEscapeCharsInUris);
            return executeExternalProcessor(PostProcessorCommandFilename, PostProcessorArguments, html);
        }

        /// <summary>
        /// Convert with an explicit native option word (flags bits 0-6 +
        /// highlight class bits 7-9). Export paths use this to bake e.g. the
        /// light syntect theme into the body without touching the shared
        /// preview snapshot. Generators without native options (Markdig
        /// fallback) ignore the word and render as usual.
        /// </summary>
        public string ConvertToHtml(string markDownText, string filepath, bool supportEscapeCharsInUris, uint nativeOptions)
        {
            var input = executeExternalProcessor(PreProcessorCommandFilename, PreProcessorArguments, markDownText);
            var optionsGenerator = markdownGenerator as INativeOptionsGenerator;
            var html = optionsGenerator != null
                ? optionsGenerator.ConvertToHtmlWithOptions(input, filepath, supportEscapeCharsInUris, nativeOptions)
                : markdownGenerator.ConvertToHtml(input, filepath, supportEscapeCharsInUris);
            return executeExternalProcessor(PostProcessorCommandFilename, PostProcessorArguments, html);
        }

        private string executeExternalProcessor(string commandFilename, string arguments, string input)
        {
            string result = input;
            if (!string.IsNullOrEmpty(commandFilename) && !string.IsNullOrEmpty(arguments))
            {
                var inputTempfilename = Path.GetTempFileName();
                var outputTempfilename = Path.GetTempFileName();
                try
                {
                    File.WriteAllText(inputTempfilename, input);
                    System.Diagnostics.Process process = new System.Diagnostics.Process();
                    System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                    startInfo.FileName = commandFilename;
                    string argumentsWithResolvedPlaceholders = arguments;
                    argumentsWithResolvedPlaceholders = argumentsWithResolvedPlaceholders.Replace(INPUT_FILENAME_PLACEHOLDER, "\"" + inputTempfilename + "\"");
                    argumentsWithResolvedPlaceholders = argumentsWithResolvedPlaceholders.Replace(OUTPUT_FILENAME_PLACEHOLDER, "\"" + outputTempfilename + "\"");
                    startInfo.Arguments = argumentsWithResolvedPlaceholders;
                    startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                    process.StartInfo = startInfo;
                    process.Start();
                    // 挂起的外部处理器不得无限期阻塞渲染链: 有界等待, 超时强杀
                    if (!process.WaitForExit(ExternalProcessorTimeoutMs))
                    {
                        try { process.Kill(); } catch (Exception) { }
                        throw new TimeoutException("Pre/Postprocessor did not exit within " + ExternalProcessorTimeoutMs + " ms and was terminated.");
                    }
                    if (File.Exists(outputTempfilename))
                    {
                        var processedOutput = File.ReadAllText(outputTempfilename);
                        result = processedOutput;
                    }
                }
                catch (Exception e)
                {
                    // e.Message 必须作为格式化参数传入, 不能拼进格式串:
                    // 异常消息含 '{'/'}' 时 string.Format 会在错误处理路径上
                    // 二次抛出 FormatException 并逃逸出本函数
                    result = string.Format("Error executing Pre/Postprocessor [{0}] with arguments [{1}]: {2}", commandFilename, arguments, e.Message);
                }
                finally
                {
                    try
                    {
                        File.Delete(inputTempfilename);
                        File.Delete(outputTempfilename);
                    } catch (Exception)
                    {

                    }
                }

            }
            return result;
        }

    }
}
