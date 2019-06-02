using System.Collections.Generic;
using System.Linq;

namespace Builder.Models
{
    public class TocDeclaredInGroup
    {
        public bool Attached { get; }
        public ApiReferenceTocSection.ApiReferenceTocSectionDeclaredIn DeclaredIn { get; }
        public List<ApiReferenceTocItem> Items { get; private set; }

        public TocDeclaredInGroup(ApiReferenceTocSection.ApiReferenceTocSectionDeclaredIn declaredIn, List<ApiReferenceTocItem> items, bool attached = false)
        {
            DeclaredIn = declaredIn;
            Items = items;
            Attached = attached;
        }

        public void SortItems()
        {
            Items = Items.OrderBy(e => e.Titles.IndexTitle.ToLowerInvariant()).ToList();
        }
    }
}