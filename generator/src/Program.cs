using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Builder.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Builder
{
    public class Program
    {
        public static int Main(string[] args)
        {
            if (args == null || args.Length < 2)
            {
                Console.Error.WriteLine($"Requires at least two arguments: the path to the root directory, and the base URL of the hosted docs");
                return 1;
            }

            var rootPath = Path.GetFullPath(args[0]);
            if (!Directory.Exists(rootPath))
            {
                Console.Error.WriteLine($"Root path '{rootPath}' does not exist");
                return 1;
            }

            var baseUrl = args[1];

            var outputPath = Path.Combine(rootPath, "generated");
            if (args.Length == 3)
                outputPath = args[2];

            var settings = new BuilderSettings(rootPath: rootPath,
                                               outputPath: outputPath,
                                               generateReport: args.Any(e => e == "--report"),
                                               baseUrl: baseUrl);

            // Set up DI
            var services = new ServiceCollection().AddLogging()
                                                  .AddSingleton(settings)
                                                  .AddSingleton<ReferenceMap>()
                                                  .AddSingleton<Outline>()
                                                  .AddSingleton<OutputPath>()
                                                  .AddSingleton<ApiDocuments>()
                                                  .AddSingleton<ApiIndicies>()
                                                  .AddSingleton<ContentPreformatter>()
                                                  .AddSingleton<MarkdownFormatter>()
                                                  .AddSingleton<ArticleRenderer>()
                                                  .AddSingleton<ApiDocumentRenderer>()
                                                  .AddSingleton<ApiIndexRenderer>()
                                                  .AddSingleton<ApiTocRenderer>()
                                                  .AddSingleton<TableOfContentsOrganizer>()
                                                  .AddSingleton<LinkCollector>()
                                                  .AddSingleton<LinkVerifier>()
                                                  .AddSingleton<Layout>()
                                                  .AddSingleton<ReportGenerator>()
                                                  .AddSingleton<DocBuilder>()
                                                  .BuildServiceProvider();

            // Set up logging
            services.GetRequiredService<ILoggerFactory>()
                    .AddProvider(new SimpleConsoleLoggerProvider(args.Any(e => e == "--debug") ? LogLevel.Trace : LogLevel.Information));

            // Run the builder
            try
            {
                services.GetRequiredService<DocBuilder>().GenerateAsync().Wait();
                return 0;
            }
            catch (AggregateException e)
            {
                Console.Error.WriteLine($"Generator failed:");
                foreach (var inner in e.InnerExceptions)
                {
                    Console.Error.WriteLine(inner);
                }
                return 1;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Generator failed: {e}");
                return 1;
            }
        }

        private class SimpleConsoleLoggerProvider : ILoggerProvider
        {
            private readonly ConcurrentDictionary<string, SimpleConsoleLogger> _loggers = new ConcurrentDictionary<string, SimpleConsoleLogger>();
            private readonly LogLevel _minLevel;

            public SimpleConsoleLoggerProvider(LogLevel minLevel)
            {
                _minLevel = minLevel;
            }

            public ILogger CreateLogger(string name)
            {
                return _loggers.GetOrAdd(name, CreateLoggerImplementation);
            }

            private SimpleConsoleLogger CreateLoggerImplementation(string name)
            {
                return new SimpleConsoleLogger(name, FilterEvents, false);
            }

            private bool FilterEvents(string logger, LogLevel logLevel)
            {
                return logLevel >= _minLevel;
            }

            public void Dispose() { }
        }

        public class SimpleConsoleLogger : ConsoleLogger
        {
            private static readonly Dictionary<LogLevel, string> LogLevelDisplayNames = new Dictionary<LogLevel, string>
            {
                { LogLevel.Critical, "CRT" },
                { LogLevel.Debug, "DBG" },
                { LogLevel.Error, "ERR" },
                { LogLevel.Information, "INF" },
                { LogLevel.Trace, "TRC" },
                { LogLevel.Warning, "WRN" }
            };

            private static readonly object PrintLock = new object();

            public SimpleConsoleLogger(string name, Func<string, LogLevel, bool> filter, bool includeScopes)
                    : base(name, filter, includeScopes) { }

            public override void WriteMessage(LogLevel logLevel, string logName, int eventId, string message, Exception exception)
            {
                var sb = new StringBuilder();
                sb.Append("[");
                sb.Append(LogLevelDisplayNames.ContainsKey(logLevel) ? LogLevelDisplayNames[logLevel] : logLevel.ToString("G").ToUpperInvariant());
                sb.Append("] ");

                sb.Append($"{message}");

                if (exception != null)
                {
                    sb.Append($" {exception}");
                }

                lock (PrintLock)
                {
                    if (logLevel >= LogLevel.Warning) System.Console.Error.WriteLine(sb.ToString());
                    else System.Console.WriteLine(sb.ToString());
                }
            }
        }
    }
}
