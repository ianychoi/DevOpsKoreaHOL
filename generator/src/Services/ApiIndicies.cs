using Builder.Models;
using Microsoft.Extensions.Logging;

namespace Builder.Services
{
    public class ApiIndicies : ApiJsonParser<ApiReferenceIndex>
    {
        protected override string SourceDirectoryName { get; } = "indicies";
        protected override string SourceDirectoryDisplayName { get; } = "index";

        public ApiIndicies(BuilderSettings settings, ILogger<ApiIndicies> logger) : base(settings, logger) {}
    }
}