using System.Collections.Generic;

namespace Builder.Models
{
    public class ApiReferenceAttribute
    {
        public ApiReferenceId Id { get; set; }
        public ApiReferenceUri Uri { get; set; }
        public ApiReferenceTitle Titles { get; set; }
        public List<string> Parameters { get; set; } = new List<string>();
    }
}