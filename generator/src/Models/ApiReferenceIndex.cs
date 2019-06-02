using System;
using System.Collections.Generic;

namespace Builder.Models
{
    public class ApiReferenceIndex : IApiJsonDocument
    {
        public ApiReferenceEntity Root { get; set; }
        public List<ApiReferenceTocItem> Descendants { get; set; } = new List<ApiReferenceTocItem>();
        public DateTime SourceFileLastModifiedAt { get; set; } = DateTime.UtcNow;
    }
}