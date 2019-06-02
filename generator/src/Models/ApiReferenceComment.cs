using System.Collections.Generic;

namespace Builder.Models
{
    public class ApiReferenceComment
    {
        public string Brief { get; set; }
        public string Full { get; set; }
        public string Remarks { get; set; }
        public string Examples { get; set; }
        public string Ux { get; set; }
        public ApiReferenceCommentAttributes Attributes { get; set; }

        public class ApiReferenceCommentAttributes
        {
            public bool Advanced { get; set; }
            public string ScriptModule { get; set; }
            public ApiReferenceCommentAttributeScriptMethod ScriptMethod { get; set; }
            public string ScriptProperty { get; set; }
            public string ScriptEvent { get; set; }
            public ApiReferenceCommentAttributeReturns Returns { get; set; }
            public bool Published { get; set; }
            public string Topic { get; set; }
            public List<ApiReferenceCommentAttributeParameter> Parameters { get; set; } = new List<ApiReferenceCommentAttributeParameter>();
            public List<string> SeeAlso { get; set; } = new List<string>();
            public bool Deprecated { get; set; }
            public bool Experimental { get; set; }
            public bool Hidden { get; set; }
        }

        public class ApiReferenceCommentAttributeScriptMethod
        {
            public string Name { get; set; }
            public List<string> Parameters { get; set; } = new List<string>();
        }

        public class ApiReferenceCommentAttributeReturns
        {
            public string TypeHint { get; set; }
            public string Text { get; set; }
        }

        public class ApiReferenceCommentAttributeParameter
        {
            public string Name { get; set; }
            public string TypeHint { get; set; }
            public string Description { get; set; }
        }
    }
}