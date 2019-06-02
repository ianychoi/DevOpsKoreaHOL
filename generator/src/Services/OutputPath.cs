using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HtmlAgilityPack;

namespace Builder.Services
{
    public class OutputPath
    {
        private static readonly Regex IdSanitizePattern = new Regex(@"[^a-zA-Z0-9-_]", RegexOptions.Compiled);
        private static readonly Regex MultipleDashPattern = new Regex(@"-{2,}", RegexOptions.Compiled);
        private static readonly Regex TrailingNoisePattern = new Regex(@"[^a-zA-Z0-9]+$", RegexOptions.Compiled);

        private readonly string _contentRoot;
        private readonly string _metaDataRoot;
        private readonly string _baseUrl;
        private readonly ReferenceMap _referenceMap;
        private readonly LinkCollector _linkCollector;
        private readonly LinkVerifier _linkVerifier;
        private readonly Layout _layout;
        private readonly ILogger<OutputPath> _logger;
        private readonly ConcurrentDictionary<string, Func<string, string, Task<string>>> _postProcessingCallbacks = new ConcurrentDictionary<string, Func<string, string, Task<string>>>();
        private readonly ConcurrentDictionary<string, bool> _writtenContentFiles = new ConcurrentDictionary<string, bool>();

        public OutputPath(BuilderSettings settings,
                          ReferenceMap referenceMap,
                          LinkCollector linkCollector,
                          LinkVerifier linkVerifier,
                          Layout layout,
                          ILogger<OutputPath> logger)
        {
            _contentRoot = settings.OutputPath;
            _metaDataRoot = settings.RootPath;
            _baseUrl = settings.BaseUrl;
            _referenceMap = referenceMap;
            _linkCollector = linkCollector;
            _linkVerifier = linkVerifier;
            _layout = layout;
            _logger = logger;

            if (Directory.Exists(_contentRoot))
            {
                _logger.LogDebug($"Output path {_contentRoot} already exists, deleting");
                Directory.Delete(_contentRoot, true);
            }
            Directory.CreateDirectory(_contentRoot);
        }

        public Task WriteContentAsync(string relativePath, DateTime lastModifiedTime, string content)
        {
            var fullPath = Path.Combine(_contentRoot, relativePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, content);
            File.SetLastWriteTimeUtc(fullPath, lastModifiedTime);
            _writtenContentFiles.TryAdd(relativePath, true);
            return Task.FromResult(0);
        }

        public async Task WriteContentAsync(string relativePath, DateTime lastModifiedTime, string content, Func<string, string, Task<string>> postProcessingCallback)
        {
            await WriteContentAsync(relativePath, lastModifiedTime, content);
            _postProcessingCallbacks.TryAdd(relativePath, postProcessingCallback);
        }

        public async Task ApplyContentPostProcessingAsync()
        {
            var writtenFiles = _writtenContentFiles.Keys;
            var callbacks = _postProcessingCallbacks.ToDictionary(key => key.Key, value => value.Value);
            var outline = File.ReadAllText(Path.Combine(_metaDataRoot, "outline-navigation.html")); // Might be needed if we're in preview mode

            _logger.LogDebug($"Applying post processing on {writtenFiles.Count} output files");
            var sw = Stopwatch.StartNew();
            var result = await Task.WhenAll(writtenFiles.Select(e => ApplyContentPostProcessorAsync(e, outline, callbacks.ContainsKey(e) ? callbacks[e] : null)));

            var filesWithMissingLinks = result.Where(e => e.InvalidLinks.Count > 0).ToList();
            if (filesWithMissingLinks.Count > 0)
            {
                _logger.LogError($"{filesWithMissingLinks.Count} files had in total {filesWithMissingLinks.Sum(e => e.InvalidLinks.Count)} invalid/dead links in them:");
                foreach (var file in filesWithMissingLinks)
                {
                    _logger.LogError($"Missing links in file {file.Filename}:");
                    foreach (var linkedFile in file.InvalidLinks)
                    {
                        _logger.LogError($" - {linkedFile}");
                    }
                }

                throw new Exception($"Missing links found, please correct before re-running");
            }

            // Generate a sitemap.xml based on the retrieved data.
            var sitemapBuilder = new StringBuilder();
            var lastmod = DateTime.UtcNow.ToString("yyyy-MM-dd");
            sitemapBuilder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sitemapBuilder.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
            foreach (var file in result) {
                sitemapBuilder.AppendLine("\t<url>");
                sitemapBuilder.AppendLine($"\t\t<loc>{_baseUrl}{file.Filename}</loc>");
                sitemapBuilder.AppendLine($"\t\t<lastmod>{lastmod}</lastmod>");
                sitemapBuilder.AppendLine("\t\t<changefreq>daily</changefreq>");
                sitemapBuilder.AppendLine("\t</url>");
            }
            sitemapBuilder.AppendLine("</urlset>");
            File.WriteAllText(Path.Combine(_contentRoot, "sitemap.xml"), sitemapBuilder.ToString());

            _logger.LogDebug($"Applied post processing on {writtenFiles.Count} output files in {sw.Elapsed.TotalMilliseconds} ms");
        }

        private async Task<PostProcessorResult> ApplyContentPostProcessorAsync(string relativePath, string outline, Func<string, string, Task<string>> processor)
        {
            var fullPath = Path.Combine(_contentRoot, relativePath);
            var originalModificationTime = File.GetLastWriteTimeUtc(fullPath);
            var input = File.ReadAllText(fullPath);

            if (processor != null)
            {
                input = await processor.Invoke(fullPath, input);
            }

            input = await _referenceMap.FormatAsync(input, relativePath);
            input = RewriteTypePrefixedLinks(input, relativePath);
            var verificationResult = _linkVerifier.VerifyLinks(input, fullPath);

            // Perform some post processing of the document.
            var doc = new HtmlDocument();
            doc.LoadHtml(input);
            var metadata = GetDocumentMetaData(doc);
            doc = AddHeaderIds(doc);
            doc = CorrectLegacyLinks(doc);
            input = doc.DocumentNode.OuterHtml;

            input = _layout.Apply(input, outline, metadata.Title);

            File.WriteAllText(fullPath, input);
            File.SetLastWriteTimeUtc(fullPath, originalModificationTime);
            return new PostProcessorResult(relativePath, metadata.Title, verificationResult.InvalidLinks);
        }

        public Task WriteMetaDataAsync(string relativePath, string content)
        {
            var fullPath = Path.Combine(_metaDataRoot, relativePath);
            File.WriteAllText(fullPath, content);
            return Task.FromResult(0);
        }

        private string RewriteTypePrefixedLinks(string input, string currentPath)
        {
            var prefixes = new[] { "articles", "api" };
            var links = _linkCollector.GetLinksFrom(input);

            foreach (var prefix in prefixes)
            {
                var linksForPrefix = links.Where(e => e.StartsWith($"{prefix}:"))
                                          .OrderByDescending(e => e.Length);
                foreach (var link in linksForPrefix)
                {
                    var path = link.Substring(prefix.Length + 1);
                    var relativePath = RelativePathHelper.GetRelativePath(currentPath, path, null);
                    var fragmentIdentifier = "";
                    {
                        var hashIndex = relativePath.IndexOf('#');
                        if (hashIndex >= 0)
                        {
                            fragmentIdentifier = relativePath.Substring(hashIndex);
                            relativePath = relativePath.Substring(0, hashIndex);
                        }
                    }
                    if (relativePath.EndsWith(".md"))
                    {
                        relativePath = relativePath.Substring(0, relativePath.Length - 2) + "html";
                    }
                    else if (relativePath.EndsWith(".json"))
                    {
                        relativePath = relativePath.Substring(0, relativePath.Length - 4) + "html";
                    }
                    if (!relativePath.EndsWith(".html"))
                    {
                        relativePath += ".html";
                    }

                    input = input.Replace(link, relativePath + fragmentIdentifier);
                }
            }

            return input;
        }

        private DocumentMetaData GetDocumentMetaData(HtmlDocument input)
        {
            var title = input.DocumentNode.SelectSingleNode("//*[self::h1 or self::h2]")?.InnerText?.Trim() ?? "";
            return new DocumentMetaData
            {
                Title = title
            };
        }

        private HtmlDocument AddHeaderIds(HtmlDocument input)
        {
            var headers = input.DocumentNode.SelectNodes("//*[self::h1 or self::h2 or self::h3 or self::h4 or self::h5 or self::h6]");
            if (headers == null)
            {
                return input;
            }

            foreach (var header in headers)
            {
                var idAttribute = header.Attributes["id"];
                if (idAttribute != null)
                {
                    header.Attributes.Remove("id");
                }

                var id = idAttribute?.Value;
                if (string.IsNullOrEmpty(id))
                {
                    id = IdSanitizePattern.Replace(header.InnerText.Trim().ToLowerInvariant(), "-");
                    id = MultipleDashPattern.Replace(id, "-");
                    id = TrailingNoisePattern.Replace(id, "");
                }

                if (!string.IsNullOrEmpty(id))
                {
                    header.Attributes.Add("id", id);
                }
            }

            return input;
        }

        private HtmlDocument CorrectLegacyLinks(HtmlDocument input)
        {
            var anchors = input.DocumentNode.SelectNodes("//*[self::a]");
            if (anchors == null)
            {
                return input;
            }

            foreach (var anchor in anchors)
            {
                var href = anchor.Attributes["href"];
                if (href == null)
                {
                    continue;
                }
                anchor.Attributes.Remove("href");

                var url = href.Value;
                if (url.StartsWith("/examples/"))
                {
                    // If the URL is a hard-link to the old examples, redirect it to examples.fusetools.com
                    url = "https://examples.fusetools.com/" + url.Substring("/examples/".Length);
                }

                anchor.Attributes.Add("href", url);
            }

            return input;
        }

        private class PostProcessorResult
        {
            public string Filename { get; }
            public string Title { get; }
            public List<string> InvalidLinks { get; }

            public PostProcessorResult(string filename, string title, List<string> invalidLinks)
            {
                Filename = filename;
                Title = title;
                InvalidLinks = invalidLinks;
            }
        }

        private class DocumentMetaData
        {
            public string Title { get; set; }
        }
    }
}
