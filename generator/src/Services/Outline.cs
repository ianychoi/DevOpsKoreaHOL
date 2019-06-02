using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Builder.Services
{
    public class Outline : Renderer
    {
        private readonly string _sourceFile;
        private readonly string _rootPath;
        private readonly string _baseUrl;
        private const string _destinationFile = "outline-navigation.html";
        private readonly OutputPath _outputPath;
        private readonly ILogger<Outline> _logger;

        public Outline(BuilderSettings settings, OutputPath outputPath, ILogger<Outline> logger)
        {
            _sourceFile = Path.Combine(settings.RootPath, "outline");
            _rootPath = settings.RootPath;
            _baseUrl = settings.BaseUrl;
            _outputPath = outputPath;
            _logger = logger;

            if (!File.Exists(_sourceFile))
            {
                throw new FileNotFoundException($"Unable to find outline file at '{_sourceFile}'");
            }
        }

        public Task<string> BuildAsync()
        {
            var state = new OutlineGenerationState();

            var sw = Stopwatch.StartNew();
            _logger.LogDebug($"Building outline file from '{_sourceFile}'");

            var lines = File.ReadAllLines(_sourceFile);
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim().StartsWith("//") || lines[i].Trim().Length == 0) continue;
                state.LineNumber = i;
                ProcessEntry(lines[i], state);
            }

            WriteDecoration(0, state);
            _logger.LogDebug($"Outline built in {sw.Elapsed.TotalMilliseconds} ms");
            return Task.FromResult(state.Builder.ToString());
        }

        public async Task GenerateAsync()
        {
            await _outputPath.WriteMetaDataAsync(_destinationFile, await BuildAsync());
        }

        private void ProcessEntry(string line, OutlineGenerationState state)
        {
            var parts = line.Split(new[] { " : " }, StringSplitOptions.None);
            if (parts.Length != 2)
            {
                throw new ArgumentException($"Malformed entry in outline at line {state.LineNumber}: {line}");
            }

            var title = parts[0].Trim();
            var link = parts[1].Trim();

            var indentation = GetIndentation(line, state);
            WriteDecoration(indentation, state);
            VerifyHref(link, line, state);
            var href = ParseHref(link, state);

            if (state.SeenLinks.ContainsKey(href))
            {
                throw new ArgumentException($"Duplicate link found in outline on line {state.LineNumber}: {href}. Previously seen on line {state.SeenLinks[href]}.");
            }
            state.SeenLinks.Add(href, state.LineNumber);

            // Hack: we've overridden the name of the api reference index page when deserializing the JSON,
            // so reflect that here.
            if (href == "api/index.html")
            {
                href = "api/root-ns.html";
            }

            // Hack: The API reference is within a "api/" subdirectory in the submodule,
            // yet when parsing is completed it's referenced without that directory.
            if (href.ToLowerInvariant().StartsWith("api/") && href.ToLowerInvariant().EndsWith(".html"))
            {
                href = href.Substring(4);
            }

            state.AppendString(indentation + 1, $"<a href=\"{_baseUrl}{EscapeHtml(href)}\">{EscapeHtml(title)}</a>");
            state.PreviousIndentation = indentation;
        }

        private string ParseHref(string href, OutlineGenerationState state)
        {
            href = href.Trim();

            if (href.StartsWith("articles/") && href.ToLowerInvariant().EndsWith(".md"))
            {
                href = href.Substring(9); // Trim off 'articles/'
                href = href.Substring(0, href.Length - 3); // Trim off the '.md' extension
                href += ".html";
            }
            else if (href.StartsWith("api-docs/") && href.ToLowerInvariant().EndsWith(".json"))
            {
                href = href.Substring(9); // Trim off 'api-docs/'
                href = href.Substring(0, href.Length - 5); // Trim off the '.json' extension
                href += ".html";
            }
            else
            {
                throw new ArgumentException($"Invalid outline link {href} on line {state.LineNumber}");
            }

            return href;
        }

        private void VerifyHref(string href, string line, OutlineGenerationState state)
        {
            var fullPath = Path.Combine(_rootPath, href);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Link referenced in outline on line {state.LineNumber} not found: '{fullPath}'");
            }
        }

        private void WriteDecoration(int indentation, OutlineGenerationState state)
        {
            if (state.PreviousIndentation != 0 && state.PreviousIndentation == indentation)
            {
                state.AppendString(state.PreviousIndentation, "</li>");
            }

            if (state.PreviousIndentation < indentation)
            {
                for (var i = state.PreviousIndentation; i < indentation; i++)
                {
                    state.AppendString(i, $"<ul class=\"list-unstyled outline-nav outline-nav-level-{i}\">");
                    state.AppendString(i + 1, "<li>");
                }
            }
            else if (state.PreviousIndentation > indentation)
            {
                for (var i = state.PreviousIndentation; i >= indentation; i--)
                {
                    if (i != 0)
                    {
                        state.AppendString(i, "</li>");
                    }
                    if (i != indentation)
                    {
                        state.AppendString(i - 1, "</ul>");
                    }
                }

                if (indentation != 0)
                {
                    state.AppendString(indentation, "<li>");
                }
            }

            if (state.PreviousIndentation == indentation)
            {
                state.AppendString(indentation, "<li>");
            }
        }

        private int GetIndentation(string line, OutlineGenerationState state)
        {
            int i;
            for (i = 0; i < line.Length; i++)
            {
                if (line[i] == ' ')
                {
                    throw new ArgumentException($"Malformed entry in outline (mixed tabs and spaces) at line {state.LineNumber}: {line}");
                }
                if (line[i] != '\t')
                {
                    break;
                }
            }

            return i + 1;
        }

        private class OutlineGenerationState
        {
            public int LineNumber { get; set; } = 0;
            public int PreviousIndentation { get; set; } = 0;
            public Dictionary<string, int> SeenLinks { get; } = new Dictionary<string, int>();
            public StringBuilder Builder { get; } = new StringBuilder();

            public void AppendString(int indentation, string text)
            {
                for (var i = 0; i < indentation; i++)
                {
                    Builder.Append("\t");
                }
                Builder.Append($"{text}\n");
            }
        }
    }
}
