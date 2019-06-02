using System.Collections.Generic;

namespace Builder.Models
{
    public class ApiReferenceTocSection
    {
        public ApiReferenceTocSectionDeclaredIn DeclaredIn { get; set; }
        public bool IsAttached { get; set; }
        public List<ApiReferenceTocItem> Items { get; set; } = new List<ApiReferenceTocItem>();

        public class ApiReferenceTocSectionDeclaredIn
        {
            public ApiReferenceId Id { get; set; }
            public ApiReferenceUri Uri { get; set; }
            public ApiReferenceTitle Titles { get; set; }
        }
    }
}