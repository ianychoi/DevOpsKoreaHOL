using System.Collections.Generic;

namespace Builder.Models
{
    public class ApiReferenceInheritance
    {
        public ApiReferenceInheritanceNode Root { get; set; }

        public class ApiReferenceInheritanceNode
        {
            public string Uri { get; set; }
            public string Title { get; set; }
            public List<ApiReferenceInheritanceNode> Children { get; set; } = new List<ApiReferenceInheritanceNode>();
            public bool IsAncestor { get; set; }
            public bool IsCurrent { get; set; }
        }
    }
}