using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Builder.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using CommonMark;

namespace Builder.Services
{
    public class MarkdownFormatter
    {
        private readonly ReferenceMap _referenceMap;
        private readonly ILogger<MarkdownFormatter> _logger;
        private readonly ConcurrentDictionary<Guid, DeferredMarkdownQueueItem> _deferred = new ConcurrentDictionary<Guid, DeferredMarkdownQueueItem>();

        public MarkdownFormatter(ReferenceMap referenceMap, ILogger<MarkdownFormatter> logger)
        {
            _referenceMap = referenceMap;
            _logger = logger;
        }

        public Task<DeferredMarkdownRenderingResult> DeferredRenderAsync(string markdown, MarkdownPostProcessor postProcessor = null)
        {
            var id = Guid.NewGuid();
            _deferred.TryAdd(id, new DeferredMarkdownQueueItem(markdown, postProcessor));
            return Task.FromResult(new DeferredMarkdownRenderingResult(id, $"<markdown_{id}/>"));
        }

        public async Task PerformDeferredRenderingAsync()
        {
            var queue = _deferred.ToList();

            var sw = Stopwatch.StartNew();
            _logger.LogDebug($"Performing deferred markdown rendering of {queue.Count} fragments");

            foreach (var item in queue) {
                await ParseMarkdown(item.Key, item.Value.Markdown);
            }

            _logger.LogDebug($"Successfully rendered {queue.Count} markdown fragments in {sw.Elapsed.TotalMilliseconds} ms");
        }

        private async Task ParseMarkdown(Guid id, string markdown) {
            var html = CommonMarkConverter.Convert(markdown);
            await UpdateDeferredResultAsync(id, html);
        }

        private async Task UpdateDeferredResultAsync(Guid id, string html)
        {
            DeferredMarkdownQueueItem existing;
            if (!_deferred.TryGetValue(id, out existing)) return;

            // Call the post processing hook
            if (existing.PostProcessor != null)
            {
                html = await existing.PostProcessor.ProcessAsync(html);
            }
            existing.Html = html;
        }

        public Task<DeferredMarkdownUpdateResult> ApplyDeferredAsync(string input, params Guid[] ids)
        {
            // Note: We work the IDs in reverse order, as there might have been rendered some DOM
            // that contains other markdown parsing result; parsing them in reverse order ensures
            // that we never leave dangling <markdown_<id>/> tags without properly parsing them.

            foreach (var id in ids.Reverse())
            {
                DeferredMarkdownQueueItem item;
                if (!_deferred.TryGetValue(id, out item))
                {
                    throw new ArgumentException($"Tried applying deferred markdown rendering payload with id {id} - unknown id");
                }

                if (item.Html == null)
                {
                    throw new ArgumentException($"Tried applying deferred markdown rendering payload with id {id} which has not yet been rendered");
                }

                input = input.Replace($"<markdown_{id}/>", item.Html);
            }

            return Task.FromResult(new DeferredMarkdownUpdateResult(input));
        }

        private class DeferredMarkdownQueueItem
        {
            public string Markdown { get; }
            public string Html { get; set; }
            public MarkdownPostProcessor PostProcessor { get; }

            public DeferredMarkdownQueueItem(string markdown, MarkdownPostProcessor postProcessor)
            {
                Markdown = markdown;
                PostProcessor = postProcessor;
            }
        }
    }
}
