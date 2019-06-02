using System.Collections.Generic;

namespace Builder.Models
{
    public class ApiReferenceTocItem
    {
        public ApiReferenceId Id { get; set; }
        public ApiReferenceUri Uri { get; set; }
        public ApiReferenceTitle Titles { get; set; }
        public ApiReferenceComment Comment { get; set; }
        public ApiReferenceReturns Returns { get; set; }
        public List<ApiReferenceParameter> Parameters { get; set; } = new List<ApiReferenceParameter>();
        public ApiReferenceFlags Flags { get; set; }
    }
}