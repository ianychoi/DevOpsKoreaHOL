using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Builder.Services
{
    public class ReferenceMap : Renderer
    {
        private static readonly Regex KeyValueSplitPattern = new Regex(@"[\s\t]+", RegexOptions.Compiled);

        private readonly string _path;
        private readonly string _apiRoot;
        private readonly string _articlesRoot;
        private readonly ILogger<ReferenceMap> _logger;
        private Dictionary<string, string> _lookups = new Dictionary<string, string>();
        private Regex _lookupMatchPattern = null;
        private readonly ConcurrentDictionary<string, HashSet<string>> _failedLookups = new ConcurrentDictionary<string, HashSet<string>>();

        public ReferenceMap(BuilderSettings settings, ILogger<ReferenceMap> logger)
        {
            _path = Path.Combine(settings.RootPath, "reference-map");
            _apiRoot = Path.Combine(settings.RootPath, "api-docs", "api");
            _articlesRoot = Path.Combine(settings.RootPath, "articles");
            _logger = logger;

            if (!File.Exists(_path))
            {
                throw new FileNotFoundException($"Unable to find reference map at '{_path}'");
            }
        }

        public string GetTarget(string keyword)
        {
            return _lookups.ContainsKey(keyword) ? _lookups[keyword] : null;
        }

        public Task ParseAsync()
        {
            var sw = Stopwatch.StartNew();
            _logger.LogDebug($"Parsing reference map at '{_path}'");
            
            var lookups = new Dictionary<string, string>();

            var lines = File.ReadAllLines(_path);
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim().StartsWith("//") || lines[i].Trim().Length == 0) continue;
                ProcessMapping(lines[i].Trim(), i, lookups);
            }

            _logger.LogDebug($"{lookups.Count} entries read from reference map in {sw.Elapsed.TotalMilliseconds} ms");
            _lookups = lookups;
            _lookupMatchPattern = BuildMatchPattern(lookups);
            return Task.FromResult(0);
        }

        public Task<string> FormatAsync(string input, string currentPath)
        {
            Match match;
            var i = 0;
            do
            {
                i++;
                match = _lookupMatchPattern.Match(input);
                if (!match.Success) break;

                var exactMatch = match.Groups[0].Value;
                var keyword = exactMatch.TrimStart('@');
                var title = keyword;

                // Hack: If the match is within a code block, replace the leading @ symbol with __CODE_BLOCK_SYMBOL_AT__ and then replace that back
                // after the run. This ensures that we don't mess with code blocks, and by replacing the @ we also make sure we don't get stuck
                // in an infinite loop here where it will just continue to match on this unhandled match every iteration.
                if (IsWithinCodeBlock(match.Index, input))
                {
                    input = input.Substring(0, match.Index) + "__CODE_BLOCK_SYMBOL_AT__" + input.Substring(match.Index + 1);
                    continue;
                }

                // Handle legacy links @(Keyword) and @(Keyword:title)
                if (keyword.StartsWith("(") && keyword.EndsWith(")"))
                {
                    keyword = keyword.TrimStart('(').TrimEnd(')');
                    title = keyword;

                    if (title.Contains(":"))
                    {
                        keyword = keyword.Substring(0, keyword.IndexOf(":"));
                        title = title.Substring(title.IndexOf(":") + 1);
                    }
                }

                if (!_lookups.ContainsKey(keyword))
                {
                    _failedLookups.AddOrUpdate(exactMatch, new HashSet<string>(new[] { currentPath }), (kw, e) =>
                    {
                        e.Add(currentPath);
                        return e;
                    });
                    input = ReplaceReferenceMatch(match, input, title);
                    continue;
                }

                var uri = _lookups[keyword];
                var replacement = $"<a href=\"{EscapeHtml(RelativePathHelper.GetRelativePath(currentPath, uri, "html"))}\">{EscapeHtml(title)}</a>";
                input = ReplaceReferenceMatch(match, input, replacement);

                // Just some stuff to test that this works since it's such a fucked up implementation
                if (i > 10000)
                {
                    throw new Exception($"Problems parsing references in {currentPath} - inifinite recursion bug: {input}");
                }
            } while (true);

            // Replace any __CODE_BLOCK_SYMBOL_AT__ values with @ to correct the content
            input = input.Replace("__CODE_BLOCK_SYMBOL_AT__", "@");

            return Task.FromResult(input);
        }

        public Dictionary<string, string[]> GetFailedLookups()
        {
            return _failedLookups.ToDictionary(key => key.Key, value => value.Value.ToArray());
        }

        private string ReplaceReferenceMatch(Match match, string input, string replaceWith)
        {
            var before = input.Substring(0, match.Index);
            var after = input.Substring(match.Index + match.Groups[0].Value.Length);
            return before + replaceWith + after;
        }

        private bool IsWithinCodeBlock(int index, string str)
        {
            if (index < 6) return false;

            for (var i = index - 6; i >= 0; i--)
            {
                if (str.Substring(i, 6) == "</code")
                {
                    return false;
                }
                if (str.Substring(i, 5) == "<code")
                {
                    return true;
                }
            }

            return false;
        }

        private void ProcessMapping(string line, int lineNumber, Dictionary<string, string> target)
        {
            var parts = KeyValueSplitPattern.Split(line, 2);
            if (parts.Length != 2)
            {
                throw new ArgumentException($"Malformed reference map entry on line {lineNumber}: {line}");
            }

            if (target.ContainsKey(parts[0]))
            {
                _logger.LogWarning($"Duplicate reference map entry on line {lineNumber}: {line}");
                return;
            }

            if (!File.Exists(Path.Combine(_articlesRoot, $"{parts[1]}.md")) && !File.Exists(Path.Combine(_apiRoot, $"{parts[1]}.json")))
            {
                throw new Exception($"Unable to resolve path {parts[1]} in reference map entry on line {lineNumber}: {line}");
            }

            target.Add(parts[0], parts[1]);
        }

        private Regex BuildMatchPattern(Dictionary<string, string> lookups)
        {
            var patterns = new List<string>();
            foreach (var kw in lookups.Keys.OrderByDescending(e => e.Length))
            {
                patterns.Add(BuildMatchPattern(kw));
            }

            // Fallback pattern, matching things that we don't have rules for explicitly
            patterns.Add("@([a-zA-Z0-9_]{1,})");
            patterns.Add("@(\\([a-zA-Z0-9_]{1,}\\))"); // Legacy reference map links
            patterns.Add("@(\\([a-zA-Z0-9_]{1,}\\:(.+?)\\))"); // Legacy reference map links with custom alias

            var pattern = $"({string.Join("|", patterns)})";
            return new Regex(pattern, RegexOptions.Compiled);
        }

        private string BuildMatchPattern(string keyword)
        {
            var patterns = new List<string>
            {
                Regex.Escape($"@{keyword}"),
                Regex.Escape($"@({keyword})"), // Legacy reference map links
                $"{Regex.Escape("@(")}{Regex.Escape(keyword)}\\:(.+?){Regex.Escape(")")}" // Legacy reference map links with custom alias
            };

            return string.Join("|", patterns);
        }
    }
}