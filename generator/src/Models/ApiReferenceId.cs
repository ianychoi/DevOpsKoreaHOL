using System.Collections.Generic;

namespace Builder.Models
{
    public class ApiReferenceId
    {
        public string Id { get; set; }
        public string ParentId { get; set; }
        public string Type { get; set; }
        public List<string> Modifiers { get; set; } = new List<string>();
    }
}