using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Builder.Models;
using Microsoft.Extensions.Logging;

namespace Builder.Services
{
    public class ArticleRenderer : Renderer
    {
        private readonly string _sourcePath;
        private readonly MarkdownFormatter _markdown;
        private readonly ContentPreformatter _preformatter;
        private readonly OutputPath _outputPath;
        private readonly ILogger<ArticleRenderer> _logger;
        private readonly ConcurrentBag<ArticleQualityReport> _qualityReports = new ConcurrentBag<ArticleQualityReport>();

        public ArticleRenderer(BuilderSettings settings,
                               MarkdownFormatter markdown,
                               ContentPreformatter preformatter,
                               OutputPath outputPath,
                               ILogger<ArticleRenderer> logger)
        {
            _sourcePath = Path.Combine(settings.RootPath, "articles");
            _markdown = markdown;
            _preformatter = preformatter;
            _outputPath = outputPath;
            _logger = logger;

            if (!Directory.Exists(_sourcePath))
            {
                throw new DirectoryNotFoundException($"Unable to find article root directory '{_sourcePath}'");
            }
        }

        public List<ArticleQualityReport> GetQualityReport()
        {
            return _qualityReports.ToList();
        }

        public async Task RenderAsync()
        {
            var sw = Stopwatch.StartNew();

            _logger.LogDebug($"Reading articles from '{_sourcePath}'");
            var files = Directory.EnumerateFiles(_sourcePath, "*.md", SearchOption.AllDirectories).ToList();
            _logger.LogDebug($"{files.Count} article source files identified");

            await Task.WhenAll(files.Select(RenderArticleAsync));

            _logger.LogDebug($"{files.Count} article source files rendered in {sw.Elapsed.TotalMilliseconds} ms");
        }

        private async Task RenderArticleAsync(string path)
        {
            var outputPath = path.Substring(_sourcePath.Length + 1);
            outputPath = outputPath.Substring(0, outputPath.Length - 3) + ".html"; // Replace '.md' extension

            var raw = File.ReadAllText(path);
            var numberOfLines = CountNumberOfLines(raw);

            var markdown = await _preformatter.ApplyPreformattingAsync(raw, outputPath);
            var markdownIds = markdown.DeferredMarkdownIds;

            var formatted = await _markdown.DeferredRenderAsync(markdown.Markdown);
            markdownIds.Add(formatted.Id);

            await _outputPath.WriteContentAsync(outputPath,
                                                File.GetLastWriteTimeUtc(path),
                                                formatted.Html,
                                                (p, i) => ApplyArticlePostProcessingAsync(p, i, markdownIds));
            _qualityReports.Add(new ArticleQualityReport(outputPath, numberOfLines));
        }

        private async Task<string> ApplyArticlePostProcessingAsync(string path, string input, List<Guid> deferredMarkdownIds)
        {
            var result = await _markdown.ApplyDeferredAsync(input, deferredMarkdownIds.ToArray());
            var html = result.Html;

            // Rewrite all links to point to their .html version instead of the original .md version
            html = html.Replace(".md)", ".html)")
                       .Replace(".md\"", ".html\"")
                       .Replace(".md#", ".html#");

            // HACK: Articles are nested one level shallower than in the source-dir compared to images, correct for that
            html = html.Replace("<img src=\"../", "<img src=\"");

            return html;
        }
    }
}
