using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Builder.Models;
using Microsoft.Extensions.Logging;

namespace Builder.Services
{
    public class ApiDocumentRenderer : Renderer
    {
        private readonly MarkdownFormatter _markdown;
        private readonly ContentPreformatter _preformatter;
        private readonly TableOfContentsOrganizer _tocOrganizer;
        private readonly ApiTocRenderer _tocRenderer;
        private readonly ReferenceMap _referenceMap;
        private readonly OutputPath _outputPath;
        private readonly ILogger<ApiDocumentRenderer> _logger;
        private readonly ConcurrentBag<ApiDocumentQualityReport> _qualityReports = new ConcurrentBag<ApiDocumentQualityReport>();

        public ApiDocumentRenderer(ContentPreformatter preformatter,
                                   MarkdownFormatter markdown,
                                   TableOfContentsOrganizer tocOrganizer,
                                   ApiTocRenderer tocRenderer,
                                   ReferenceMap referenceMap,
                                   OutputPath outputPath,
                                   ILogger<ApiDocumentRenderer> logger)
        {
            _markdown = markdown;
            _preformatter = preformatter;
            _tocOrganizer = tocOrganizer;
            _tocRenderer = tocRenderer;
            _referenceMap = referenceMap;
            _outputPath = outputPath;
            _logger = logger;
        }

        public List<ApiDocumentQualityReport> GetQualityReport()
        {
            return _qualityReports.ToList();
        }

        public async Task RenderAsync(IList<ApiReferenceDocument> documents)
        {
            var sw = Stopwatch.StartNew();
            var entityCache = documents.ToDictionary(key => key.Entity.Id.Id, value => value.Entity);

            _logger.LogDebug($"Rendering {documents.Count} API documents");
            await Task.WhenAll(documents.Select(e => RenderDocumentAsync(e, entityCache)));
            _logger.LogDebug($"{documents.Count} API documents rendered in {sw.Elapsed.TotalMilliseconds} ms");
        }

        private async Task RenderDocumentAsync(ApiReferenceDocument document, Dictionary<string, ApiReferenceEntity> entityCache)
        {
            // Don't render documents for swizzler types
            if (document.Entity.Id.Type == "SwizzlerType")
                return;

            var filename = NormalizePath($"{document.Entity.Uri.Href}.html");

            // Hack: If this is the root document, renname it to root-ns.html so we can use another
            // index document within the articles directory.
            if (document.Entity.Id.Type == "Root" && document.Entity.Id.Id == "__root__")
            {
                filename = "root-ns.html";
            }

            var state = new RenderingState(filename, new StringBuilder(), document, entityCache);

            WriteMetaData(state);
            WriteTitle(state);
            WriteSubsectionLinks(state);
            WriteNotifications(state);
            WriteAttachedAttributeDetails(state);
            await WriteCommentAsync(state);
            await WriteUxAsync(state);
            WriteLocation(state);
            await WriteParametersAsync(state);
            await WriteReturnsAsync(state);
            await WriteValuesAsync(state);
            await WriteTocAsync(state);
            await WriteInterfacesAsync(state);
            await WriteRemarksAsync(state);
            await WriteExamplesAsync(state);
            await WriteSeeAlsoAsync(state);

            await _outputPath.WriteContentAsync(filename,
                                                document.SourceFileLastModifiedAt,
                                                state.Builder.ToString(),
                                                (p, i) => ApplyDocumentPostProcessingAsync(p, i, state.MarkdownDeferredIds));
            _qualityReports.Add(new ApiDocumentQualityReport(document, state.Quality.NumberOfCommentLines, state.Quality.TableOfContentsCommentLines));
        }

        private void WriteMetaData(RenderingState state)
        {
        }

        private void WriteTitle(RenderingState state)
        {
            var title = GetPageTitle(state.Document, state.EntityCache);
            var subTitle = "";
            var hasToc = (state.Document.TableOfContents?.Count ?? 0) > 0;
            var onlyAdvanced = state.Document.TableOfContents.HasOnlyAdvancedItems(state.Document.Entity.Id);

            // Allow topic comment to override the title, and use the existing title as a subtitle instead
            if (!string.IsNullOrWhiteSpace(state.Document.Entity.Comment?.Attributes?.Topic))
            {
                subTitle = title;
                title = state.Document.Entity.Comment.Attributes.Topic;
            }

            state.AppendString($"<header class=\"page-header\">");
            state.AppendString($"<h2 id=\"section-introduction\">");
            state.AppendString(EscapeHtml(title));
            if (!string.IsNullOrWhiteSpace(subTitle))
            {
                state.AppendString($"<span class=\"sub-title\">{EscapeHtml(subTitle)}</span>");
            }
            state.AppendString($"</h2>");

            if (hasToc)
            {
                state.AppendString($"<form class=\"advanced-toggle\">");
                state.AppendString($"<div class=\"form-check\">");
                if (onlyAdvanced) state.AppendString($"<input class=\"form-check-input\" type=\"checkbox\" id=\"showAdvancedCheckbox\" checked />");
                else state.AppendString($"<input class=\"form-check-input\" type=\"checkbox\" id=\"showAdvancedCheckbox\" />");
                state.AppendString($"<label class=\"form-check-label\" for=\"showAdvancedCheckbox\">");
                state.AppendString($"Show advanced things");
                state.AppendString($"</label>");
                state.AppendString($"</div>");
                state.AppendString($"</form>");
            }
            state.AppendString($"</header>");

            if (hasToc && onlyAdvanced)
            {
                state.AppendString($"<div class=\"alert alert-info alert-api-advanced-only\">");
                state.AppendString($"This page contains documentation for advanced Fuse features, so we have ");
                state.AppendString($"taken the liberty to tick the \"Show advanced things\" checkbox above for ");
                state.AppendString($"you in advance to be able to provide you with some additional information.");
                state.AppendString($"</div>");
            }
        }

        private void WriteSubsectionLinks(RenderingState state)
        {
            var sections = new List<KeyValuePair<string, string>>();
            if (!string.IsNullOrWhiteSpace(state.Document.Entity.Comment?.Ux)) sections.Add(new KeyValuePair<string, string>("ux", "UX"));
            if (state.Document.TableOfContents.Count > 0) sections.Add(new KeyValuePair<string, string>("table-of-contents", "Table of Contents"));
            if (!string.IsNullOrWhiteSpace(state.Document.Entity.Comment?.Remarks)) sections.Add(new KeyValuePair<string, string>("remarks", "Remarks"));
            if (!string.IsNullOrWhiteSpace(state.Document.Entity.Comment?.Examples)) sections.Add(new KeyValuePair<string, string>("examples", "Examples"));
            if ((state.Document.Entity.Comment?.Attributes?.SeeAlso?.Count ?? 0) > 0) sections.Add(new KeyValuePair<string, string>("see-also", "See Also"));

            if (sections.Count == 0) return;

            state.AppendString($"<section class=\"section-jump\">");
            state.AppendString($"<ul class=\"nav nav-pills\">");
            state.AppendString($"<li class=\"nav-item\"><a href=\"#\" class=\"nav-link disabled\">Jump to:</a></li>");

            foreach (var section in sections)
            {
                state.AppendString($"<li class=\"nav-item\"><a href=\"#section-{EscapeHtml(section.Key)}\" class=\"nav-link\">{EscapeHtml(section.Value)}</a></li>");
            }

            state.AppendString($"</ul>");
            state.AppendString($"</section>");
        }

        private void WriteNotifications(RenderingState state)
        {
            if (state.Document.Entity.Comment?.Attributes?.Deprecated ?? false)
            {
                state.AppendString($"<section class=\"notifications\">");
                state.AppendString($"<div class=\"alert alert-warning alert-api-deprecated\">");
                state.AppendString($"This entity is deprecated and will be removed in a future release.");
                state.AppendString($"</div>");
                state.AppendString($"</section>");
            }
            else if (state.Document.Entity.Comment?.Attributes?.Experimental ?? false)
            {
                state.AppendString($"<section class=\"notifications\">");
                state.AppendString($"<div class=\"alert alert-warning alert-api-experimental\">");
                state.AppendString($"This entity is experimental and might be changed or removed in a future release.");
                state.AppendString($"</div>");
                state.AppendString($"</section>");
            }
        }

        private void WriteAttachedAttributeDetails(RenderingState state)
        {
            var attachedInfo = ApiRenderingHelper.GetAttachedAttributeInfo(state.Document.Entity.Id,
                                                                           state.Document.Entity.Titles,
                                                                           state.Document.Entity.Parameters,
                                                                           state.Document.Entity.Attributes,
                                                                           state.EntityCache);
            if (attachedInfo == null) return;

            state.AppendString($"<p><em>");
            state.AppendString($"Attached by <a href=\"{EscapeHtml(RelativePathHelper.GetRelativePath(state.Document.Entity.Uri.Href, attachedInfo.AttachedByHref, "html"))}\">{EscapeHtml(attachedInfo.AttachedByType)}</a>.");
            state.AppendString($"Use full name <code>{EscapeHtml(attachedInfo.FullName)}</code> in UX markup if ambiguous.");
            state.AppendString("</em></p>");
        }

        private async Task WriteCommentAsync(RenderingState state)
        {
            if (string.IsNullOrWhiteSpace(state.Document.Entity.Comment?.Full)) return;
            state.Quality.NumberOfCommentLines = CountNumberOfLines(state.Document.Entity.Comment.Full);

            var comment = await _preformatter.ApplyPreformattingAsync(state.Document.Entity.Comment.Full, state.Document.Entity.Uri.Href);
            state.MarkdownDeferredIds.AddRange(comment.DeferredMarkdownIds);

            var parserResult = await RenderMarkdownAsync(comment.Markdown);
            state.MarkdownDeferredIds.Add(parserResult.Id);
            state.AppendString(parserResult.Html);
        }

        private async Task WriteUxAsync(RenderingState state)
        {
            if (string.IsNullOrWhiteSpace(state.Document.Entity.Comment?.Ux)) return;
            await WriteMarkdownSectionAsync(state, "ux", "UX", state.Document.Entity.Comment.Ux);
        }

        private void WriteLocation(RenderingState state)
        {
            if (string.IsNullOrWhiteSpace(state.Document.Entity.Location?.NamespaceUri) && string.IsNullOrWhiteSpace(state.Document.Entity.Location?.PackageName)) return;

            var cssClasses = new List<string> { "type-location" };
            if (state.Document.TableOfContents.Count == 0) cssClasses.Add("type-location-leaf");

            state.AppendString($"<section class=\"{string.Join(" ", cssClasses)}\">");
            state.AppendString($"<h3 id=\"section-location\">Location</h3>");
            state.AppendString($"<dl>");

            if (!string.IsNullOrWhiteSpace(state.Document.Entity.Location.NamespaceUri))
            {
                state.AppendString($"<dt>Namespace</dt>");
                state.AppendString($"<dd>");
                state.AppendString($"<a href=\"{EscapeHtml(RelativePathHelper.GetRelativePath(state.Document.Entity.Uri.Href, state.Document.Entity.Location.NamespaceUri, "html"))}\">");
                state.AppendString(EscapeHtml(state.Document.Entity.Location.NamespaceTitle));
                state.AppendString($"</a>");
                state.AppendString($"</dd>");
            }

            if (!string.IsNullOrWhiteSpace(state.Document.Entity.Location.PackageName))
            {
                state.AppendString($"<dt>Package</dt>");
                state.AppendString($"<dd>{EscapeHtml(state.Document.Entity.Location.PackageName + " " + state.Document.Entity.Location.PackageVersion)}</dd>");
            }

            state.AppendString($"</dl>");
            state.AppendString($"</section>");
        }

        private async Task WriteParametersAsync(RenderingState state)
        {
            var parameters = BuildParameterCollection(state.Document.Entity);
            if (parameters.Count == 0) return;

            state.AppendString($"<section class=\"parameters\">");
            state.AppendString($"<h3 id=\"section-parameters\">Parameters</h3>");
            state.AppendString($"<dl>");

            foreach (var param in parameters)
            {
                state.AppendString($"<dt>{EscapeHtml(param.Name)}</dt>");
                state.AppendString($"<dd>");

                if (!string.IsNullOrWhiteSpace(param.ReturnsTitle))
                {
                    state.AppendString($"<p>");

                    if (!string.IsNullOrWhiteSpace(param.ReturnsHref))
                    {
                        state.AppendString($"<a href=\"{EscapeHtml(RelativePathHelper.GetRelativePath(state.Document.Entity.Uri.Href, param.ReturnsHref, "html"))}\">");
                    }

                    state.AppendString(EscapeHtml(param.ReturnsTitle));

                    if (!string.IsNullOrWhiteSpace(param.ReturnsHref))
                    {
                        state.AppendString($"</a>");
                    }

                    state.AppendString("</p>");
                }
                else if (!string.IsNullOrWhiteSpace(param.TypeHint))
                {
                    state.AppendString($"<p>{EscapeHtml(param.TypeHint)}</p>");
                }

                if (!string.IsNullOrWhiteSpace(param.Comment))
                {
                    var result = await RenderMarkdownAsync(param.Comment);
                    state.MarkdownDeferredIds.Add(result.Id);
                    state.AppendString(result.Html);
                }

                state.AppendString($"</dd>");
            }

            state.AppendString($"</dl>");
            state.AppendString($"</section>");
        }

        private async Task WriteReturnsAsync(RenderingState state)
        {
            var returns = BuildReturns(state.Document.Entity);
            if (returns == null) return;

            state.AppendString($"<section class=\"returns\">");
            state.AppendString($"<h3 id=\"section-returns\">Returns</h3>");
            state.AppendString($"<p>");

            if (!string.IsNullOrWhiteSpace(returns.ReturnsTitle))
            {
                if (!string.IsNullOrWhiteSpace(returns.ReturnsHref))
                {
                    state.AppendString($"<a href=\"{EscapeHtml(RelativePathHelper.GetRelativePath(state.Document.Entity.Uri.Href, returns.ReturnsHref, "html"))}\">");
                }

                state.AppendString(EscapeHtml(returns.ReturnsTitle));

                if (!string.IsNullOrWhiteSpace(returns.ReturnsHref))
                {
                    state.AppendString($"</a>");
                }
            }
            else if (!string.IsNullOrWhiteSpace(returns.TypeHint))
            {
                state.AppendString(EscapeHtml(returns.TypeHint));
            }

            state.AppendString($"</p>");

            if (!string.IsNullOrWhiteSpace(returns.Comment))
            {
                var result = await RenderMarkdownAsync(returns.Comment);
                state.MarkdownDeferredIds.Add(result.Id);
                state.AppendString(result.Html);
            }

            state.AppendString($"</section>");
        }

        private async Task WriteValuesAsync(RenderingState state)
        {
            if ((state.Document.Entity.Values?.Count ?? 0) == 0) return;

            state.AppendString($"<section class=\"values\">");
            state.AppendString($"<h3 id=\"section-values\">Possible Values</h3>");
            state.AppendString($"<dl>");

            foreach (var value in state.Document.Entity.Values)
            {
                state.AppendString($"<dt>");
                state.AppendString($"<a href=\"{EscapeHtml(RelativePathHelper.GetRelativePath(state.Document.Entity.Uri.Href, value.Uri, "html"))}\">");
                state.AppendString(EscapeHtml(value.Title));
                state.AppendString($"</a>");
                state.AppendString($"</dt>");
                state.AppendString($"<dd>");

                if (!string.IsNullOrWhiteSpace(value.Comment?.Brief))
                {
                    var result = await RenderMarkdownAsync(value.Comment.Brief);
                    state.MarkdownDeferredIds.Add(result.Id);
                    state.AppendString(result.Html);
                }

                state.AppendString($"</dd>");
            }

            state.AppendString($"</dl>");
            state.AppendString($"</section>");
        }

        private async Task WriteTocAsync(RenderingState state)
        {
            if (state.Document.TableOfContents.Count == 0) return;

            state.AppendString($"<section class=\"table-of-contents\">");

            if (state.Document.Entity.Id.Type == "Namespace" || state.Document.Entity.Id.Type == "Root")
            {
                // Handle root namespace pages differently than others;
                // Instead of grouping things based on where they are inherited from, we instead show everything
                // in a single table split by entity type.
                var byType = _tocOrganizer.SplitByType(state.Document.TableOfContents);
                await WriteTocByTypeAsync(byType, state);
            }
            else
            {
                // Split elements within each group based on where they are inherited from
                var byDeclaredIn = _tocOrganizer.SplitByDeclaredIn(state.Document.Entity, state.Document.TableOfContents);
                await WriteTocByDeclaredInAsync(byDeclaredIn, state);
            }
            state.AppendString($"</section>");
        }

        private async Task WriteInterfacesAsync(RenderingState state)
        {
            if (state.Document.Entity.ImplementedInterfaces.Count == 0) return;

            state.AppendString($"<section class=\"interfaces\">");
            state.AppendString($"<section class=\"table-of-contents\">");
            state.AppendString($"<section class=\"table-of-contents-section has-advanced-items only-advanced-items\">");
            state.AppendString($"<h4 id=\"section-table-of-contents-implemented-interfaces\">Implemented Interfaces</h4>");

            foreach (var iface in state.Document.Entity.ImplementedInterfaces)
            {
                var result = await _tocRenderer.RenderAsync(iface.Id, iface.Uri, iface.Titles, iface.Comment, state.EntityCache, state.Document.Entity.Uri.Href, true);
                state.MarkdownDeferredIds.AddRange(result.DeferredMarkdownIds);
                state.AppendString(result.Html);
            }
            state.AppendString($"</section>");
            state.AppendString($"</section>");
            state.AppendString($"</section>");
        }

        private async Task WriteRemarksAsync(RenderingState state)
        {
            if (string.IsNullOrWhiteSpace(state.Document.Entity.Comment?.Remarks)) return;
            await WriteMarkdownSectionAsync(state, "remarks", "Remarks", state.Document.Entity.Comment.Remarks);
        }

        private async Task WriteExamplesAsync(RenderingState state)
        {
            if (string.IsNullOrWhiteSpace(state.Document.Entity.Comment?.Examples)) return;
            await WriteMarkdownSectionAsync(state, "examples", "Examples", state.Document.Entity.Comment.Examples);
        }

        private async Task WriteSeeAlsoAsync(RenderingState state)
        {
            if ((state.Document.Entity.Comment?.Attributes?.SeeAlso?.Count ?? 0) == 0) return;
            var tocItems = new List<ApiReferenceTocItem>();

            foreach (var item in state.Document.Entity.Comment.Attributes.SeeAlso)
            {
                // Use the raw referenced entity if if an entity with the right id exists,
                // otherwise attempt to look up the keyword using the reference map
                var key = item;
                if (!state.EntityCache.ContainsKey(key))
                {
                    key = _referenceMap.GetTarget(item);
                }

                if (string.IsNullOrWhiteSpace(key) || !state.EntityCache.ContainsKey(key))
                {
                    _logger.LogError($"Unable to resolve seealso entry for {state.Document.Entity.Uri.Href} - no match in file system nor reference map: '{item}'");
                    continue;
                }

                var cached = state.EntityCache[key];
                tocItems.Add(new ApiReferenceTocItem
                {
                    Id = cached.Id,
                    Uri = cached.Uri,
                    Titles = cached.Titles,
                    Comment = cached.Comment,
                    Returns = cached.Returns,
                    Parameters = cached.Parameters,
                    Flags = cached.Flags
                });
            }

            if (tocItems.Count == 0) return;

            _logger.LogInformation($"adding seealso to {state.Document.Entity.Uri.Href}");
            state.AppendString($"<section class=\"see-also\">");
            state.AppendString($"<section class=\"table-of-contents\">");
            state.AppendString($"<section class=\"table-of-contents-section\">");
            state.AppendString($"<h4 id=\"section-see-also\">See Also</h4>");

            foreach (var item in tocItems)
            {
                var result = await _tocRenderer.RenderAsync(item.Id, item.Uri, item.Titles, item.Comment, state.EntityCache, state.Document.Entity.Uri.Href, false);
                state.MarkdownDeferredIds.AddRange(result.DeferredMarkdownIds);
                state.AppendString(result.Html);
            }

            state.AppendString($"</section>");
            state.AppendString($"</section>");
            state.AppendString($"</section>");
        }

        private async Task WriteTocByTypeAsync(List<TocTypeGroup> groups, RenderingState state)
        {
            foreach (var group in groups)
            {
                state.AppendString($"<h3 id=\"section-table-of-contents\">{EscapeHtml(group.Title)}</h3>");
                state.AppendString($"<section class=\"table-of-contents-section\">");

                foreach (var item in group.Items)
                {
                    var result = await _tocRenderer.RenderAsync(item, state.EntityCache, state.Document.Entity.Uri.Href, item.IsAdvanced(state.Document.Entity.Id));
                    state.MarkdownDeferredIds.AddRange(result.DeferredMarkdownIds);
                    state.AppendString(result.Html);
                    state.Quality.TableOfContentsCommentLines.Add(item.Uri.IdUri, result.NumberOfCommentLines);
                }

                state.AppendString($"</section>");
            }
        }

        private async Task WriteTocByDeclaredInAsync(List<TocDeclaredInGroup> groups, RenderingState state)
        {
            state.AppendString($"<h3 id=\"section-table-of-contents\">");
            state.AppendString($"Interface of {EscapeHtml(state.Document.Entity.Titles.IndexTitle)}");
            state.AppendString($"</h3>");

            foreach (var group in groups)
            {
                var hasAdvanced = group.Items.Any(e => e.IsAdvanced(state.Document.Entity.Id));
                var onlyAdvanced = group.Items.All(e => e.IsAdvanced(state.Document.Entity.Id));

                var cssClasses = new List<string> { "table-of-contents-section" };
                if (hasAdvanced) cssClasses.Add("has-advanced-items");
                if (onlyAdvanced) cssClasses.Add("only-advanced-items");
                if (group.DeclaredIn != null && group.DeclaredIn.Uri != null && group.DeclaredIn.Uri.IdUri != state.Document.Entity.Uri.IdUri)
                {
                    cssClasses.Add("inherited");
                }
                if (group.Attached) cssClasses.Add("attached");

                state.AppendString($"<section class=\"{string.Join(" ", cssClasses)}\">");

                if (group.DeclaredIn != null && group.DeclaredIn.Id != null && group.DeclaredIn.Id.Id != state.Document.Entity.Id.Id)
                {
                    state.AppendString($"<h4 id=\"section-table-of-contents-inherited-from-{group.DeclaredIn.Uri.Href.Replace("/", "-")}\">");
                    state.AppendString($"Inherited from");
                    state.AppendString($"<a href=\"{EscapeHtml(RelativePathHelper.GetRelativePath(state.Document.Entity.Uri.Href, group.DeclaredIn.Uri.Href, "html"))}\">");
                    state.AppendString(EscapeHtml(group.DeclaredIn.Titles.IndexTitle));
                    state.AppendString($"</a>");
                    state.AppendString($"</h4>");
                }
                else if (group.Attached)
                {
                    state.AppendString($"<h4 id=\"section-table-of-contents-attached-ux-attributes\">Attached UX Attributes</h4>");
                }

                foreach (var item in group.Items)
                {
                    var result = await _tocRenderer.RenderAsync(item, state.EntityCache, state.Document.Entity.Uri.Href, item.IsAdvanced(state.Document.Entity.Id));
                    state.MarkdownDeferredIds.AddRange(result.DeferredMarkdownIds);
                    state.AppendString(result.Html);
                    state.Quality.TableOfContentsCommentLines.Add(item.Uri.IdUri, result.NumberOfCommentLines);
                }

                state.AppendString($"</section>");
            }
        }

        private List<GenericParameterEntry> BuildParameterCollection(ApiReferenceEntity entity)
        {
            var result = new List<GenericParameterEntry>();

            // JavaScript methods use comments to determine parameters
            if (entity.Id.Type == "JsMethod")
            {
                if ((entity.Comment?.Attributes?.ScriptMethod?.Parameters?.Count ?? 0) == 0)
                {
                    return result;
                }

                foreach (var param in entity.Comment.Attributes.ScriptMethod.Parameters)
                {
                    var comment = FindParameterComment(param, entity.Comment);
                    result.Append(new GenericParameterEntry
                    {
                        Name = param,
                        Comment = comment?.Description,
                        TypeHint = comment?.TypeHint
                    });
                }

                return result;
            }

            // JavaScript events use comments to determine parameters
            // TODO: Export doesn't have support for this yet, so just return an empty list
            if (entity.Id.Type == "JsEvent") return result;

            if ((entity.Parameters?.Count ?? 0) == 0) return result;
            foreach (var param in entity.Parameters)
            {
                var comment = FindParameterComment(param.Name, entity.Comment);
                result.Add(new GenericParameterEntry
                {
                    Name = param.Name,
                    Comment = comment?.Description,
                    ReturnsHref = param.IsVirtual ? null : param.Href,
                    ReturnsTitle = param.Title
                });
            }
            return result;
        }

        private GenericReturnsEntry BuildReturns(ApiReferenceEntity entity)
        {
            var comment = entity.Comment?.Attributes?.Returns;

            // JavaScript methods use comments to determine return Value
            if (entity.Id.Type == "JsMethod")
            {
                if (string.IsNullOrWhiteSpace(comment?.TypeHint) && string.IsNullOrWhiteSpace(comment?.Text)) return null;
                return new GenericReturnsEntry
                {
                    Comment = comment.Text,
                    TypeHint = comment.TypeHint
                };
            }

            if (entity.Returns == null) return null;
            return new GenericReturnsEntry
            {
                Comment = comment?.Text,
                ReturnsHref = entity.Returns.IsVirtual ? null : entity.Returns.Href,
                ReturnsTitle = entity.Returns.Title
            };
        }

        private ApiReferenceComment.ApiReferenceCommentAttributeParameter FindParameterComment(string name, ApiReferenceComment comment)
        {
            return comment?.Attributes?.Parameters?.FirstOrDefault(e => e.Name == name);
        }

        private async Task WriteMarkdownSectionAsync(RenderingState state, string className, string title, string markdown)
        {
            var markdownResult = await _preformatter.ApplyPreformattingAsync(markdown, state.Document.Entity.Uri.Href);
            state.MarkdownDeferredIds.AddRange(markdownResult.DeferredMarkdownIds);

            var result = await RenderMarkdownAsync(markdownResult.Markdown);
            state.MarkdownDeferredIds.Add(result.Id);

            state.AppendString($"<section class=\"documentation-{className}\">");
            state.AppendString($"<h3 id=\"section-{className}\">{EscapeHtml(title)}</h3>");
            state.AppendString(result.Html);
            state.AppendString($"</section>");
        }

        private async Task<DeferredMarkdownRenderingResult> RenderMarkdownAsync(string markdown)
        {
            return await _markdown.DeferredRenderAsync(markdown, new ApiDocumentMarkdownPostProcessor());
        }

        private string GetPageTitle(ApiReferenceDocument doc, Dictionary<string, ApiReferenceEntity> entityCache)
        {
            return ApiRenderingHelper.GetTitle(doc.Entity.Id,
                                               doc.Entity.Titles,
                                               doc.Entity.Comment,
                                               doc.Entity.Parameters,
                                               doc.Entity.Attributes,
                                               entityCache,
                                               false);
        }

        private async Task<string> ApplyDocumentPostProcessingAsync(string path, string input, List<Guid> markdownDeferredIds)
        {
            var result = await _markdown.ApplyDeferredAsync(input, markdownDeferredIds.ToArray());
            return result.Html;
        }

        private string NormalizePath(string path)
        {
            return path.Replace('/', Path.DirectorySeparatorChar);
        }

        private class ApiDocumentMarkdownPostProcessor : MarkdownPostProcessor
        {
            public override Task<string> ProcessAsync(string html)
            {
                // This is a hack to increase header levels a little in injected markdown to
                // make the pages look better (h1-h4 -> h3-h6)
                for (var i = 4; i > 0; i--)
                {
                    var prevStart = $"<h{i}";
                    var prevEnd = $"</h{i}";
                    var newStart = $"<h{i + 2}";
                    var newEnd = $"</h{i + 2}";
                    html = html.Replace(prevStart, newStart)
                            .Replace(prevEnd, newEnd);
                }

                return Task.FromResult(html);
            }
        }

        private class RenderingState
        {
            public string Path { get; }
            public StringBuilder Builder { get; }
            public ApiReferenceDocument Document { get; }
            public List<Guid> MarkdownDeferredIds { get; } = new List<Guid>();
            public Dictionary<string, ApiReferenceEntity> EntityCache { get; }
            public QualityInfo Quality { get; } = new QualityInfo();

            public RenderingState(string path, StringBuilder builder, ApiReferenceDocument document, Dictionary<string, ApiReferenceEntity> entityCache)
            {
                Path = path;
                Builder = builder;
                Document = document;
                EntityCache = entityCache;
            }

            public void AppendString(string str)
            {
                Builder.Append($"{str}\n");
            }

            public class QualityInfo
            {
                public int NumberOfCommentLines { get; set; } = 0;
                public Dictionary<string, int> TableOfContentsCommentLines { get; } = new Dictionary<string, int>();
            }
        }

        private class GenericParameterEntry
        {
            public string Name { get; set; }
            public string Comment { get; set; }
            public string TypeHint { get; set; }
            public string ReturnsHref { get; set; }
            public string ReturnsTitle { get; set; }
        }

        private class GenericReturnsEntry
        {
            public string Comment { get; set; }
            public string TypeHint { get; set; }
            public string ReturnsHref { get; set; }
            public string ReturnsTitle { get; set; }
        }
    }
}
