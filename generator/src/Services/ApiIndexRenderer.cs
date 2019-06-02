using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Builder.Models;
using Microsoft.Extensions.Logging;

namespace Builder.Services
{
    public class ApiIndexRenderer : Renderer
    {
        private static readonly Regex SubclassPattern = new Regex(@"\[subclass ([\w\.]+)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly ApiIndicies _apiIndicies;
        private readonly ApiTocRenderer _tocRenderer;
        private Dictionary<string, ApiReferenceIndex> _indexById = new Dictionary<string, ApiReferenceIndex>();
        private readonly ILogger<ApiIndexRenderer> _logger;

        public ApiIndexRenderer(ApiIndicies apiIndicies, ApiTocRenderer tocRenderer, ILogger<ApiIndexRenderer> logger)
        {
            _apiIndicies = apiIndicies;
            _tocRenderer = tocRenderer;
            _logger = logger;
        }

        public async Task LoadIndiciesAsync()
        {
            var result = await _apiIndicies.ReadAsync();
            _indexById = result.ToDictionary(key => key.Root.Uri.Href, value => value);
        }

        public async Task<EmbeddedSubclassIndexResult> EmbedSubclassIndicies(string doc, string currentPath)
        {
            var markdownIds = new List<Guid>();
            var matches = SubclassPattern.Matches(doc);
            foreach (Match match in matches)
            {
                var exactMatch = match.Groups[0].Value;
                var href = match.Groups[1].Value;
                var result = await RenderSubclassIndexAsync(href, currentPath);
                if (result == null)
                {
                    doc = doc.Replace(exactMatch, "");
                    continue;
                }

                markdownIds.AddRange(result.Item2);
                doc = doc.Replace(exactMatch, result.Item1);
            }

            return new EmbeddedSubclassIndexResult(doc, markdownIds);
        }

        private async Task<Tuple<string, List<Guid>>> RenderSubclassIndexAsync(string href, string path)
        {
            // Transform the href so it's in the format we expect
            href = href.ToLowerInvariant().Replace(".", "/");
            var markdownIds = new List<Guid>();

            if (!_indexById.ContainsKey(href))
            {
                _logger.LogWarning($"Unable to find API index for {href} - ignoring");
                return null;
            }

            var sb = new StringBuilder();
            sb.Append($"<section class=\"table-of-contents\">\n");
            sb.Append($"<section class=\"table-of-contents-section\">\n");

            var items = _indexById[href].Descendants
                                        .Where(e => !e.Id.Modifiers.Contains("abstract"))
                                        .OrderBy(e => e.Titles.IndexTitle.ToLowerInvariant());
            foreach (var item in items)
            {
                var result = await _tocRenderer.RenderAsync(item, new Dictionary<string, ApiReferenceEntity>(), path, item.IsAdvanced(null));
                markdownIds.AddRange(result.DeferredMarkdownIds);
                sb.Append(result.Html);
            }

            sb.Append($"</section>\n");
            sb.Append($"</section>\n");

            return new Tuple<string, List<Guid>>(sb.ToString(), markdownIds);
        }

        public class EmbeddedSubclassIndexResult
        {
            public string Html { get; }
            public List<Guid> DeferredMarkdownIds { get; }

            public EmbeddedSubclassIndexResult(string html, List<Guid> deferredMarkdownIds)
            {
                Html = html;
                DeferredMarkdownIds = deferredMarkdownIds;
            }
        }
    }
}
