using System.Collections.Generic;

namespace Builder.Models
{
    public class ApiDocumentQualityReport
    {
        public ApiReferenceDocument Document { get; }
        public int NumberOfCommentLines { get; }
        public Dictionary<string, int> TableOfContentsQuality { get; }

        public ApiDocumentQualityReport(ApiReferenceDocument document, int numberOfCommentLines, Dictionary<string, int> tableOfContentsQuality)
        {
            Document = document;
            NumberOfCommentLines = numberOfCommentLines;
            TableOfContentsQuality = tableOfContentsQuality;
        }
    }
}