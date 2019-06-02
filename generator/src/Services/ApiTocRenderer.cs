using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Builder.Models;
using Microsoft.Extensions.Logging;

namespace Builder.Services
{
    public class ApiTocRenderer : Renderer
    {
        private readonly MarkdownFormatter _markdown;
        private readonly ILogger<ApiTocRenderer> _logger;

        public ApiTocRenderer(MarkdownFormatter markdown, ILogger<ApiTocRenderer> logger)
        {
            _markdown = markdown;
            _logger = logger;
        }

        public Task<ApiTocRenderingResult> RenderAsync(ApiReferenceTocItem item,
                                                       Dictionary<string, ApiReferenceEntity> entityCache,
                                                       string currentPath,
                                                       bool isAdvanced)
        {
            return RenderAsync(item.Id, item.Uri, item.Titles, item.Flags, item.Returns, item.Comment, entityCache, currentPath, isAdvanced);
        }

        public Task<ApiTocRenderingResult> RenderAsync(ApiReferenceId id,
                                                       ApiReferenceUri uri,
                                                       ApiReferenceTitle title,
                                                       ApiReferenceComment comment,
                                                       Dictionary<string, ApiReferenceEntity> entityCache,
                                                       string currentPath,
                                                       bool isAdvanced)
        {
            return RenderAsync(id, uri, title, null, null, comment, entityCache, currentPath, isAdvanced);
        }

        public async Task<ApiTocRenderingResult> RenderAsync(ApiReferenceId id,
                                                             ApiReferenceUri uri,
                                                             ApiReferenceTitle title,
                                                             ApiReferenceFlags flags,
                                                             ApiReferenceReturns returns,
                                                             ApiReferenceComment comment,
                                                             Dictionary<string, ApiReferenceEntity> entityCache,
                                                             string currentPath,
                                                             bool isAdvanced)
        {
            var state = new RenderingState(new StringBuilder(), id, uri, title, flags, returns, comment, entityCache, currentPath);

            var cssClasses = new List<string> { "table-of-contents-item" };
            if (isAdvanced) cssClasses.Add("is-advanced");

            state.AppendString($"<article class=\"{string.Join(" ", cssClasses)}\">");
            WriteType(state);
            WriteLink(state);
            await WriteCommentAsync(state);
            state.AppendString($"</article>");

            return new ApiTocRenderingResult(state.Builder.ToString(), state.DeferredMarkdownIds, state.NumberOfCommentLines);
        }

        private void WriteType(RenderingState state)
        {
            var cssClasses = new List<string>
            {
                "table-of-contents-item-type",
                $"table-of-contents-item-type-{state.Id.Type.ToLowerInvariant()}",
                "fa",
                $"fa-{GetTypeIcon(state)}"
            };

            state.AppendString($"<span class=\"{string.Join(" ", cssClasses)}\" title=\"{EscapeHtml(state.Id.Type)}\"></span>");
        }

        private void WriteLink(RenderingState state)
        {
            var title = ApiRenderingHelper.GetTitle(state.Id, state.Title, state.Comment, null, null, new Dictionary<string, ApiReferenceEntity>(), true);
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException($"No title could be generated for {state.Id.Id} in {state.CurrentPath} (type {state.Id.Type})");
            }

            state.AppendString($"<h5>");
            state.AppendString($"<a href=\"{EscapeHtml(RelativePathHelper.GetRelativePath(state.CurrentPath, state.Uri.Href, "html"))}\">");
            state.AppendString(EscapeHtml(title));
            state.AppendString($"</a>");
            WriteAttachedBy(state);
            WriteReturns(state);
            WriteLanguage(state);
            state.AppendString($"</h5>");
        }

        private async Task WriteCommentAsync(RenderingState state)
        {
            if (string.IsNullOrWhiteSpace(state.Comment?.Brief)) return;
            state.NumberOfCommentLines = CountNumberOfLines(string.IsNullOrWhiteSpace(state.Comment?.Full) ? "" : state.Comment.Full);

            var brief = state.Comment.Brief;
            if (state.Comment.Brief != state.Comment.Full)
            {
                brief += $" <a href=\"{EscapeHtml(RelativePathHelper.GetRelativePath(state.CurrentPath, state.Uri.Href, "html"))}\" class=\"table-of-contents-item-has-more\" title=\"There is more information available for this entry\"><i class=\"fa fa-ellipsis-h\"></i></a>";
            }

            state.AppendString($"<div class=\"table-of-contents-item-brief\">");
            var parsed = await _markdown.DeferredRenderAsync(brief);
            state.DeferredMarkdownIds.Add(parsed.Id);
            state.AppendString(parsed.Html);

            state.AppendString($"</div>");
        }

        private void WriteAttachedBy(RenderingState state)
        {
            if (!state.Id.Type.StartsWith("AttachedUx")) return;

            if (!state.EntityCache.ContainsKey(state.Uri.Href))
            {
                throw new ArgumentException($"TOC item {state.Uri.Href} was not found in entity cache");
            }
            var underlyingEntity = state.EntityCache[state.Uri.Href];
            if (!state.EntityCache.ContainsKey(underlyingEntity.Id.ParentId))
            {
                throw new ArgumentException($"Parent of TOC item {state.Id.Id} ({underlyingEntity.Id.ParentId} was not found in entity cache");
            }
            var attachedBy = state.EntityCache[underlyingEntity.Id.ParentId];

            var attachedByName = attachedBy.Titles.IndexTitle;
            var attachedByHref = attachedBy.Uri.Href;

            state.AppendString($"<span class=\"table-of-contents-item-inline-attached-by\">");
            state.AppendString($"(attached by <a href=\"{EscapeHtml(RelativePathHelper.GetRelativePath(state.CurrentPath, attachedByHref, "html"))}\">{EscapeHtml(attachedByName)}</a>)");
            state.AppendString($"</span>");
        }

        private void WriteReturns(RenderingState state)
        {
            if (state.Id.Type == "JsMethod" || state.Returns == null) return;

            state.AppendString($"<span class=\"table-of-contents-item-inline-returns\"> : ");

            if (!string.IsNullOrWhiteSpace(state.Returns.Href) && !state.Returns.IsVirtual)
            {
                state.AppendString($"<a href=\"{EscapeHtml(RelativePathHelper.GetRelativePath(state.CurrentPath, state.Returns.Href, "html"))}\">");
            }

            var title = state.Returns.Title;

            // If this is a List<T> or IList<T>, transform the return value to read "List of T" or "IList of T"
            if (title.StartsWith("List<") || title.StartsWith("IList<"))
            {
                var prefix = title.Substring(0, title.IndexOf("<"));
                var generics = title.Substring(prefix.Length + 1).TrimEnd('>');
                title = $"{prefix} of {generics}";
            }

            state.AppendString(EscapeHtml(title));

            if (!string.IsNullOrWhiteSpace(state.Returns.Href) && !state.Returns.IsVirtual)
            {
                state.AppendString($"</a>");
            }

            state.AppendString($"</span>");
        }

        private void WriteLanguage(RenderingState state)
        {
            string language;
            switch (state.Id.Type.ToLowerInvariant())
            {
                case "jsmodule":
                case "jsproperty":
                case "jsevent":
                case "jsmethod":
                    language = "js";
                    break;

                case "uxclass":
                case "uxproperty":
                case "uxevent":
                case "attacheduxproperty":
                case "attacheduxevent":
                    language = "ux";
                    break;

                default:
                    language = "uno";
                    break;
            }

            var cssClasses = new List<string>
            {
                "table-of-contents-item-language",
                $"table-of-contents-item-language-{language}"
            };

            state.AppendString($"<span class=\"{string.Join(" ", cssClasses)}\">{EscapeHtml(language)}</span>");
        }

        private string GetTypeIcon(RenderingState state)
        {
            switch (state.Id.Type.ToLowerInvariant())
            {
                case "property":
                case "uxproperty":
                case "jsproperty":
                case "attacheduxproperty":
                    if (state.Flags != null && (state.Flags.UxComponents || state.Flags.UxContent || state.Flags.UxPrimary))
                    {
                        return (state.Returns?.Href ?? "").StartsWith("uno/collections/list_") || (state.Returns?.Href ?? "").StartsWith("uno/collections/ilist_")
                                 ? "cubes"
                                 : "cube";
                    }
                    return "wrench";

                case "event":
                case "uxevent":
                case "jsevent":
                case "attacheduxevent":
                    return "bolt";

                case "jsmethod":
                case "method":
                    return "square-o";

                case "constructor":
                    return "check";

                case "literal":
                    return "tag";

                case "class":
                case "uxclass":
                case "jsmodule":
                    return "cog";

                case "namespace":
                    return "gears";

                case "enum":
                    return "tags";

                case "struct":
                    return "th";

                case "delegate":
                    return "arrow-circle-o-right";

                case "interface":
                    return "file-o";

                case "operator":
                    return "asterisk";

                case "field":
                    return "circle-o";

                case "cast":
                    return "repeat";

                case "swizzlertype":
                    return "files-o";

                default:
                    throw new ArgumentException($"Unable to identify TOC type icon for {state.Id.Id} with type {state.Id.Type}");
            }
        }

        private class RenderingState
        {
            public StringBuilder Builder { get; }
            public ApiReferenceId Id { get; }
            public ApiReferenceUri Uri { get; }
            public ApiReferenceTitle Title { get; }
            public ApiReferenceFlags Flags { get ;}
            public ApiReferenceReturns Returns { get; }
            public ApiReferenceComment Comment { get; }
            public string CurrentPath { get; }
            public List<Guid> DeferredMarkdownIds { get; } = new List<Guid>();
            public Dictionary<string, ApiReferenceEntity> EntityCache { get; }
            public int NumberOfCommentLines { get; set; } = 0;

            public RenderingState(StringBuilder builder,
                                  ApiReferenceId id,
                                  ApiReferenceUri uri,
                                  ApiReferenceTitle title,
                                  ApiReferenceFlags flags,
                                  ApiReferenceReturns returns,
                                  ApiReferenceComment comment,
                                  Dictionary<string, ApiReferenceEntity> entityCache,
                                  string currentPath)
            {
                Builder = builder;
                Id = id;
                Uri = uri;
                Title = title;
                Flags = flags;
                Returns = returns;
                Comment = comment;
                EntityCache = entityCache;
                CurrentPath = currentPath;
            }

            public void AppendString(string str)
            {
                Builder.Append($"{str}\n");
            }
        }

        public class ApiTocRenderingResult
        {
            public string Html { get; }
            public List<Guid> DeferredMarkdownIds { get; }
            public int NumberOfCommentLines { get; }

            public ApiTocRenderingResult(string html, List<Guid> deferredMarkdownIds, int numberOfCommentLines)
            {
                Html = html;
                DeferredMarkdownIds = deferredMarkdownIds;
                NumberOfCommentLines = numberOfCommentLines;
            }
        }
    }
}
