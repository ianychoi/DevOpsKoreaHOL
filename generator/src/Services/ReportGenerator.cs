using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Builder.Models;
using Microsoft.Extensions.Logging;

namespace Builder.Services
{
    public class ReportGenerator : Renderer
    {
        private static readonly HashSet<string> ExcludedApiDocumentTypes = new HashSet<string>(new[]
        {
            "Constructor",
            "Property",
            "UxProperty",
            "Method",
            "Event",
            "UxEvent",
            "Field",
            "Cast",
            "Operator",
            "Literal",
        });
        private const int MinCommentLineThreshold = 4;
        private const int MinArticleLineThreshold = 4;

        private readonly string _path;
        private readonly ReferenceMap _referenceMap;
        private readonly ApiDocumentRenderer _apiDocumentRenderer;
        private readonly ArticleRenderer _articleRenderer;
        private readonly ILogger<ReportGenerator> _logger;

        public ReportGenerator(BuilderSettings settings,
                               ReferenceMap referenceMap,
                               ApiDocumentRenderer apiDocumentRenderer,
                               ArticleRenderer articleRenderer,
                               ILogger<ReportGenerator> logger)
        {
            _path = Path.Combine(settings.RootPath, "generator-report.html");
            _referenceMap = referenceMap;
            _apiDocumentRenderer = apiDocumentRenderer;
            _articleRenderer = articleRenderer;
            _logger = logger;
        }

        public void Build()
        {
            _logger.LogInformation($"Generating report at {_path}");

            var state = new RenderingState();
            state.Append($"<!DOCTYPE html>");
            state.Append($"<html lang=\"en\">");
            state.Append($"<head>");
            state.Append($"<meta charset=\"utf-8\">");
            state.Append($"<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">");
            state.Append($"<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            state.Append($"<title>Generator report</title>");
            state.Append($"<link rel=\"stylesheet\" href=\"https://maxcdn.bootstrapcdn.com/bootstrap/3.3.7/css/bootstrap.min.css\" integrity=\"sha384-BVYiiSIFeK1dGmJRAkycuHAHRg32OmUcww7on3RYdg4Va+PmSTsz/K68vbdEjh4u\" crossorigin=\"anonymous\">");
            state.Append($"</head>");
            state.Append($"<body>");
            state.Append($"<div class=\"container\">");

            state.Append($"<div class=\"jumbotron\">");
            state.AppendTitle($"Generator report");
            state.Append($"<p class=\"lead\">Generated at {DateTime.Now.ToString("G")}</p>");
            state.Append($"</div>");

            AppendLowQualityTypes(state);
            AppendLowQualityArticles(state);
            AppendReferenceMapReport(state);

            state.Append($"</div>");
            state.Append($"</body>");
            state.Append($"</html>");

            File.WriteAllText(_path, state.Builder.ToString());
        }

        private void AppendLowQualityTypes(RenderingState state)
        {
            state.AppendSubTitle($"Low quality API documentation");
            var report = _apiDocumentRenderer.GetQualityReport()
                                             .Where(e => !ExcludedApiDocumentTypes.Contains(e.Document.Entity.Id.Type))
                                             .Where(e => e.NumberOfCommentLines < MinCommentLineThreshold || e.TableOfContentsQuality.Any(x => x.Value < MinCommentLineThreshold))
                                             .ToList();

            if (report.Count == 0)
            {
                state.Append($"<div class=\"alert alert-success\">No low quality API documentation found.</div>");
            }
            else
            {
                state.Append($"<div class=\"alert alert-warning\">{report.Count} data types had low quality documentation.</div>");
                state.Append($"<table class=\"table table-bordered table-striped table-condensed\">");
                state.Append($"<thead>");
                state.Append($"<tr>");
                state.Append($"<th>Data Type</th>");
                state.Append($"<th>Quality Info</th>");
                state.Append($"</tr>");
                state.Append($"</thead>");
                state.Append($"<tbody>");
                var itemIndex = 0;
                foreach (var item in report)
                {
                    var truncatedName = item.Document.Entity.Titles.FullyQualifiedIndexTitle;
                    if (truncatedName.Length > 100)
                    {
                        truncatedName = "..." + truncatedName.Substring(truncatedName.Length - 100);
                    }
                    state.Append($"<tr>");
                    state.Append($"<td>");
                    state.Append(itemIndex.ToString());
                    state.Append($"</td>");
                    state.Append($"<td>");
                    state.Append($"<a href=\"generated/{EscapeHtml(item.Document.Entity.Uri.Href)}.html\">{EscapeHtml(truncatedName)}</a>");
                    state.Append($"<span class=\"label label-default\">{EscapeHtml(item.Document.Entity.Id.Type)}</span>");
                    state.Append($"</td>");
                    state.Append($"<td>");
                    state.Append($"<ul>");

                    if (item.NumberOfCommentLines == 0)
                    {
                        state.Append($"<li>No docs available.</li>");
                    }
                    else if (item.NumberOfCommentLines < MinCommentLineThreshold)
                    {
                        state.Append($"<li>Only {item.NumberOfCommentLines} lines of docs available.</li>");
                    }

                    var tocMissingCount = item.TableOfContentsQuality.Count(e => e.Value == 0);
                    if (tocMissingCount > 0)
                    {
                        state.Append($"<li>{tocMissingCount} nested items had no docs available.</li>");
                    }

                    var tocPoorQualityCount = item.TableOfContentsQuality.Count(e => e.Value > 0 && e.Value < MinCommentLineThreshold);
                    if (tocPoorQualityCount > 0)
                    {
                        state.Append($"<li>{tocPoorQualityCount} nested items had less than {MinCommentLineThreshold} lines of docs.</li>");
                    }

                    state.Append($"</ul>");
                    state.Append($"</td>");
                    state.Append($"</tr>");

                    itemIndex++;
                }
                state.Append($"</tbody>");
                state.Append($"</table>");
            }
        }

        private void AppendLowQualityArticles(RenderingState state)
        {
            state.AppendSubTitle($"Low quality articles");
            var report = _articleRenderer.GetQualityReport()
                                         .Where(e => e.NumberOfLines < MinArticleLineThreshold)
                                         .ToList();

            if (report.Count == 0)
            {
                state.Append($"<div class=\"alert alert-success\">No low quality articles found.</div>");
            }
            else
            {
                state.Append($"<div class=\"alert alert-warning\">{report.Count} articles had low quality content.</div>");
                state.Append($"<table class=\"table table-bordered table-striped table-condensed\">");
                state.Append($"<thead>");
                state.Append($"<tr>");
                state.Append($"<th>Path</th>");
                state.Append($"<th>Quality Info</th>");
                state.Append($"</tr>");
                state.Append($"</thead>");
                state.Append($"<tbody>");
                foreach (var item in report)
                {
                    state.Append($"<tr>");
                    state.Append($"<td>");
                    state.Append($"<a href=\"generated/{EscapeHtml(item.Path)}\">{EscapeHtml(item.Path)}</a>");
                    state.Append($"</td>");
                    state.Append($"<td>");
                    state.Append($"<ul>");

                    if (item.NumberOfLines == 0)
                    {
                        state.Append($"<li>No content available.</li>");
                    }
                    else if (item.NumberOfLines < MinArticleLineThreshold)
                    {
                        state.Append($"<li>Only {item.NumberOfLines} lines of content available.</li>");
                    }

                    state.Append($"</ul>");
                    state.Append($"</td>");
                    state.Append($"</tr>");
                }
                state.Append($"</tbody>");
                state.Append($"</table>");
            }
        }

        private void AppendReferenceMapReport(RenderingState state)
        {
            state.AppendSubTitle($"Reference map lookups");
            var failed = _referenceMap.GetFailedLookups();
            if (failed.Count == 0)
            {
                state.Append($"<div class=\"alert alert-success\">No reference map lookup failures detected.</div>");
            }
            else
            {
                state.Append($"<div class=\"alert alert-warning\">{failed.Sum(e => e.Value.Length)} reference map lookup failures detected.</div>");
                state.Append($"<table class=\"table table-bordered table-striped table-condensed\">");
                state.Append($"<thead>");
                state.Append($"<tr>");
                state.Append($"<th>Keyword</th>");
                state.Append($"<th>Referenced in</th>");
                state.Append($"</tr>");
                state.Append($"</thead>");
                state.Append($"<tbody>");
                foreach (var reference in failed.Keys.OrderBy(e => e.ToLower()))
                {
                    state.Append($"<tr>");
                    state.Append($"<td>{EscapeHtml(reference)}</td>");
                    state.Append($"<td>");
                    state.Append($"<ul>");
                    foreach (var path in failed[reference])
                    {
                        state.Append($"<li><a href=\"generated/{EscapeHtml(path)}\">{EscapeHtml(path)}</a></li>");
                    }
                    state.Append($"</ul>");
                    state.Append($"</td>");
                    state.Append($"</tr>");
                }
                state.Append($"</tbody>");
                state.Append($"</table>");
            }
        }

        private class RenderingState : Renderer
        {
            public StringBuilder Builder { get; } = new StringBuilder();

            public void Append(string str)
            {
                Builder.Append($"{str}\n");
            }

            public void AppendTitle(string title)
            {
                Builder.Append($"<h1>{EscapeHtml(title)}</h1>");
            }

            public void AppendSubTitle(string title)
            {
                Builder.Append($"<h2>{EscapeHtml(title)}</h2>");
            }
        }

        private class ApiDocumentWithQualityDetails
        {
            public ApiReferenceDocument Document { get; }
            public int NumberOfCommentLines { get; }
            public List<int> TocNumberOfCommentLines { get; }

            public ApiDocumentWithQualityDetails(ApiReferenceDocument document, int numberOfCommentLines, List<int> tocNumberOfCommentLines)
            {
                Document = document;
                NumberOfCommentLines = numberOfCommentLines;
                TocNumberOfCommentLines = tocNumberOfCommentLines;
            }
        }
    }
}
