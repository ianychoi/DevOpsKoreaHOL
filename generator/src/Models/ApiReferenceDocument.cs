using System;
using System.Collections.Generic;

namespace Builder.Models
{
    public class ApiReferenceDocument : IApiJsonDocument
    { 
        public ApiReferenceEntity Entity { get; set; }
        public Dictionary<string, List<ApiReferenceTocSection>> TableOfContents { get; set; } = new Dictionary<string, List<ApiReferenceTocSection>>();
        public DateTime SourceFileLastModifiedAt { get; set; } = DateTime.UtcNow;
    }
}