using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Builder.Services
{
    public class LinkVerifier
    {

        private readonly LinkCollector _collector;
        private readonly ILogger<LinkVerifier> _logger;
        private readonly string _documentsRoot;

        public LinkVerifier(BuilderSettings settings, LinkCollector collector, ILogger<LinkVerifier> logger)
        {
            _documentsRoot = settings.OutputPath;
            _collector = collector;
            _logger = logger;
        }

        public LinkVerificationResult VerifyLinks(string input, string currentPath)
        {
            var invalid = new List<string>();
            var links = _collector.GetLinksFrom(input);
            foreach (var href in links)
            {
                var isValid = IsValidLink(href, currentPath);
                if (!isValid)
                {
                    invalid.Add(href);
                }
            }
            return new LinkVerificationResult(invalid.Count == 0, invalid);
        }

        private bool IsValidLink(string href, string currentPath)
        {
            if (href.StartsWith("#")) return true; // Ignore document relative anchors
            if (href.StartsWith("/")) return true; // Ignore absolute paths
            if (href.StartsWith("file://") || href.StartsWith("http://") || href.StartsWith("https://") || href.StartsWith("mailto:")) return true; // Ignore absolute URIs

            // Strip anchors from the Path
            if (href.Contains("#"))
            {
                href = href.Substring(0, href.IndexOf("#"));
            }

            var cwd = Path.GetDirectoryName(currentPath);
            var fullPath = Path.GetFullPath(Path.Combine(cwd, href.Replace('/', Path.DirectorySeparatorChar)));
            return File.Exists(fullPath);
        }

        public class LinkVerificationResult
        {
            public bool IsValid { get; }
            public List<string> InvalidLinks { get; }

            public LinkVerificationResult(bool isValid, List<string> invalidLinks)
            {
                IsValid = isValid;
                InvalidLinks = invalidLinks;
            }
        }
    }
}
