using System;

namespace Builder
{
    public class BuilderSettings
    {
        public string RootPath { get; }
        public string OutputPath { get; }
        public bool GenerateReport { get; }
        public string BaseUrl { get; }

        public BuilderSettings(string rootPath, string outputPath, bool generateReport, string baseUrl)
        {
            RootPath = rootPath;
            OutputPath = outputPath;
            GenerateReport = generateReport;
            BaseUrl = baseUrl;
        }
    }
}
