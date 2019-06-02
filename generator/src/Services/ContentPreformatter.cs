using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Builder.Services
{
    public class ContentPreformatter
    {
        private readonly ApiIndexRenderer _indexRenderer;
        private readonly ILogger<ContentPreformatter> _logger;

        public ContentPreformatter(ApiIndexRenderer indexRenderer,
                                   ILogger<ContentPreformatter> logger)
        {
            _indexRenderer = indexRenderer;
            _logger = logger;
        }

        public async Task<ContentPreformattingResult> ApplyPreformattingAsync(string markdown, string currentPath)
        {
            var result = await _indexRenderer.EmbedSubclassIndicies(markdown, currentPath);
            return new ContentPreformattingResult(result.Html, result.DeferredMarkdownIds);
        }

        public class ContentPreformattingResult
        {
            public string Markdown { get; }
            public List<Guid> DeferredMarkdownIds { get; }

            public ContentPreformattingResult(string markdown, List<Guid> deferredMarkdownIds)
            {
                Markdown = markdown;
                DeferredMarkdownIds = deferredMarkdownIds;
            }
        }
    }
}