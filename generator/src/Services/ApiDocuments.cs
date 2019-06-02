using System.Linq;
using Builder.Models;
using Microsoft.Extensions.Logging;

namespace Builder.Services
{
    public class ApiDocuments : ApiJsonParser<ApiReferenceDocument>
    {
        protected override string SourceDirectoryName { get; } = "api";
        protected override string SourceDirectoryDisplayName { get; } = "document";

        public ApiDocuments(BuilderSettings settings, ILogger<ApiDocuments> logger) : base(settings, logger) {}

        protected override TModel NormalizeModel<TModel>(TModel model)
        {
            foreach (var pair in model.TableOfContents)
            {
                foreach (var section in pair.Value)
                {
                    section.Items = section.Items.Where(e => e.Comment?.Attributes == null || !e.Comment.Attributes.Hidden).ToList();
                }
            }

            return model;
        }
    }
}