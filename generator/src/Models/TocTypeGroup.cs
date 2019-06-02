using System.Collections.Generic;

namespace Builder.Models
{
    public class TocTypeGroup
    {
        public string Type { get; }
        public string Title { get; }
        public List<ApiReferenceTocItem> Items { get; }

        public TocTypeGroup(string type, string title, List<ApiReferenceTocItem> items)
        {
            Type = type;
            Title = title;
            Items = items;
        }
    }
}