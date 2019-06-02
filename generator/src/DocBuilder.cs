using System.Threading.Tasks;
using Builder.Services;
using Microsoft.Extensions.Logging;

namespace Builder
{
    public class DocBuilder
    {
        private readonly BuilderSettings _settings;
        private readonly ReferenceMap _referenceMap;
        private readonly Outline _outline;
        private readonly ApiDocuments _apiDocuments;
        private readonly ArticleRenderer _articleRenderer;
        private readonly ApiDocumentRenderer _apiDocumentRenderer;
        private readonly ApiIndexRenderer _apiIndexRenderer;
        private readonly OutputPath _outputPath;
        private readonly MarkdownFormatter _markdownFormatter;
        private readonly ReportGenerator _reportGenerator;
        private readonly ILogger<DocBuilder> _logger;

        public DocBuilder(BuilderSettings settings,
                          ReferenceMap referenceMap,
                          Outline outline,
                          ApiDocuments apiDocuments,
                          ArticleRenderer articleRenderer,
                          ApiDocumentRenderer apiDocumentRenderer,
                          ApiIndexRenderer apiIndexRenderer,
                          OutputPath outputPath,
                          MarkdownFormatter markdownFormatter,
                          ReportGenerator reportGenerator,
                          ILogger<DocBuilder> logger)
        {
            _settings = settings;
            _referenceMap = referenceMap;
            _outline = outline;
            _apiDocuments = apiDocuments;
            _articleRenderer = articleRenderer;
            _apiDocumentRenderer = apiDocumentRenderer;
            _apiIndexRenderer = apiIndexRenderer;
            _outputPath = outputPath;
            _markdownFormatter = markdownFormatter;
            _reportGenerator = reportGenerator;
            _logger = logger;
        }

        public async Task GenerateAsync()
        {
            _logger.LogInformation($"Starting generation of docs from root path {_settings.RootPath}");

            // Prepare rendering by parsing meta data files and loading API JSON payloads
            var apiDocumentsTask = _apiDocuments.ReadAsync();
            await Task.WhenAll(_referenceMap.ParseAsync(),
                               _outline.GenerateAsync(),
                               _apiIndexRenderer.LoadIndiciesAsync(),
                               apiDocumentsTask);
            var apiDocuments = await apiDocumentsTask;

            // Render documentation to disk
            await Task.WhenAll(_articleRenderer.RenderAsync(),
                               _apiDocumentRenderer.RenderAsync(apiDocuments));

            // Render all markdown fragments that have been queued up after rendering
            // and run a post processor path on the output files for any files that have
            // post processing queued up.
            await _markdownFormatter.PerformDeferredRenderingAsync();
            await _outputPath.ApplyContentPostProcessingAsync();

            if (_settings.GenerateReport)
            {
                _reportGenerator.Build();
            }
        }
    }
}
