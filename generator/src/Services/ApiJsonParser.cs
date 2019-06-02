using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Builder.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Builder.Services
{
    public abstract class ApiJsonParser<T> where T : class, IApiJsonDocument, new()
    {
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        private readonly string _sourcePath;
        private readonly ILogger<ApiJsonParser<T>> _logger;

        protected abstract string SourceDirectoryName { get; }
        protected abstract string SourceDirectoryDisplayName { get; }

        protected ApiJsonParser(BuilderSettings settings, ILogger<ApiJsonParser<T>> logger)
        {
            _sourcePath = Path.Combine(settings.RootPath, "api-docs", SourceDirectoryName);
            _logger = logger;

            if (!Directory.Exists(_sourcePath))
            {
                throw new DirectoryNotFoundException($"API {SourceDirectoryDisplayName} directory '{_sourcePath}' not found");
            }
        }
        
        protected virtual TModel NormalizeModel<TModel>(TModel model) where TModel : T
        {
            return model;
        }

        public Task<List<T>> ReadAsync()
        {
            var sw = Stopwatch.StartNew();
            var result = new List<T>();
            
            _logger.LogDebug($"Reading API {SourceDirectoryDisplayName} from '{_sourcePath}'");
            var files = Directory.EnumerateFiles(_sourcePath, "*.json", SearchOption.AllDirectories).ToList();
            _logger.LogDebug($"{files.Count} API {SourceDirectoryDisplayName} source files identified");

            foreach (var file in files)
            {
                result.Add(Deserialize(file));
            }

            _logger.LogDebug($"{result.Count} API {SourceDirectoryDisplayName} source files parsed in {sw.Elapsed.TotalMilliseconds} ms");

            return Task.FromResult(result);
        }

        private T Deserialize(string path)
        {
            try
            {
                var model = JsonConvert.DeserializeObject<T>(File.ReadAllText(path), SerializerSettings);
                model.SourceFileLastModifiedAt = File.GetLastWriteTimeUtc(path);
                return NormalizeModel(model);
            }
            catch (JsonReaderException e)
            {
                _logger.LogError($"Failed to parse '{path}'");
                throw;
            }
        }
    }
}
