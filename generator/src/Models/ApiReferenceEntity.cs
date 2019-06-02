using System.Collections.Generic;

namespace Builder.Models
{
    public class ApiReferenceEntity
    {
        public ApiReferenceId Id { get; set; }
        public ApiReferenceUri Uri { get; set; }
        public ApiReferenceTitle Titles { get; set; }
        public ApiReferenceComment Comment { get; set; }
        public ApiReferenceLocation Location { get; set; }
        public ApiReferenceBase Base { get; set; }
        public ApiReferenceInheritance Inheritance { get; set; }
        public List<ApiReferenceParameter> Parameters { get; set; } = new List<ApiReferenceParameter>();
        public ApiReferenceReturns Returns { get; set; }
        public List<ApiReferenceInterface> ImplementedInterfaces { get; set; } = new List<ApiReferenceInterface>();
        public List<ApiReferenceValue> Values { get; set; } = new List<ApiReferenceValue>();
        public ApiReferenceFlags Flags { get; set; }
        public List<ApiReferenceAttribute> Attributes { get; set; } = new List<ApiReferenceAttribute>();
    }
}