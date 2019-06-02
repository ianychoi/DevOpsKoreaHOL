using System;
using System.Collections.Generic;
using System.Linq;
using Builder.Models;
using Microsoft.Extensions.Logging;

namespace Builder.Services
{
    public class TableOfContentsOrganizer
    {
        private static readonly List<TocTypeDefinition> TocItemTypes = new List<TocTypeDefinition>
        {
            new TocTypeDefinition("attachedUxProperties", "Attached UX Properties", 1),
            new TocTypeDefinition("attachedUxEvents", "Attached UX Events", 2),
            
            new TocTypeDefinition("jsModules", "JavaScript Modules", 3),
            new TocTypeDefinition("jsProperties", "JavaScript Properties", 4),
            new TocTypeDefinition("jsEvents", "JavaScript Events", 5),

            new TocTypeDefinition("namespaces", "Namespaces", 6),

            new TocTypeDefinition("uxClasses", "UX Classes", 7),
            new TocTypeDefinition("classes", "Classes", 8),
            new TocTypeDefinition("delegates", "Delegates", 9),
            new TocTypeDefinition("enums", "Enums", 10),
            new TocTypeDefinition("interfaces", "Interfaces", 11),
            new TocTypeDefinition("structs", "Structs", 12),

            new TocTypeDefinition("constructors", "Constructors", 13),
            new TocTypeDefinition("properties", "Properties", 14),
            new TocTypeDefinition("methods", "Methods", 15),
            new TocTypeDefinition("events", "Events", 16),
            new TocTypeDefinition("fields", "Fields", 17),
            new TocTypeDefinition("casts", "Casts", 18),
            new TocTypeDefinition("operators", "Operators", 19),
            new TocTypeDefinition("literals", "Literals", 20),
            new TocTypeDefinition("swizzlerTypes", "Swizzler Types", 21)
        };

        private readonly ILogger<TableOfContentsOrganizer> _logger;

        public TableOfContentsOrganizer(ILogger<TableOfContentsOrganizer> logger)
        {
            _logger = logger;
        }

        public List<TocDeclaredInGroup> SplitByDeclaredIn(ApiReferenceEntity entity,
                                                          Dictionary<string, List<ApiReferenceTocSection>> toc)
        {
            var debug = entity.Uri.Href == "fuse/controls/control";

            var byParent = new Dictionary<string, DeclaredInAndItems>();
            var parentsByPriority = new List<string>();
            if (entity.Inheritance != null)
            {
                parentsByPriority = FlattenInheritanceTree(entity.Inheritance.Root, parentsByPriority);
            }

            foreach (var group in toc)
            {
                byParent = GroupByDeclaredIn(entity, group.Value, byParent);
            }

            // Build a properly sorted list of sections based on the parent list
            var sections = new List<TocDeclaredInGroup>();

            // Add the section for "self" first
            if (byParent.ContainsKey(entity.Uri.Href))
            {
                var section = new TocDeclaredInGroup(null, byParent[entity.Uri.Href].Items);
                sections.Add(section);
            }
            var parentsByPriorityWithoutSelf = parentsByPriority.Where(e => e != entity.Uri.Href).ToList();

            for (var i = parentsByPriorityWithoutSelf.Count - 1; i >= 0; i--)
            {
                if (!byParent.ContainsKey(parentsByPriorityWithoutSelf[i])) continue;
                var current = byParent[parentsByPriorityWithoutSelf[i]];
                var section = new TocDeclaredInGroup(current.DeclaredIn, current.Items);

                if (section.DeclaredIn == null)
                {
                    throw new Exception($"Got section without DeclaredIn for parent {parentsByPriorityWithoutSelf[i]} inside {entity.Uri.Href}");
                }

                sections.Add(section);
            }

            // Pick out the attached items from all sections and move them into a separate section
            var attachedSection = new TocDeclaredInGroup(null, new List<ApiReferenceTocItem>(), true);
            foreach (var section in sections)
            {
                for (var i = section.Items.Count - 1; i >= 0; i--)
                {
                    var item = section.Items[i];
                    if (item.Id.Type.StartsWith("Attached"))
                    {
                        attachedSection.Items.Add(item);
                        section.Items.RemoveAt(i);
                    }
                }
            }
            if (attachedSection.Items.Count > 0)
            {
                sections.Add(attachedSection);
            }

            // Remove empty sections
            sections = sections.Where(e => e.Items.Count > 0).ToList();

            // Sort the items in all sections
            foreach (var section in sections)
            {
                section.SortItems();
            }

            return sections;
        }

        public List<TocTypeGroup> SplitByType(Dictionary<string, List<ApiReferenceTocSection>> toc)
        {
            var groups = new List<TocTypeGroup>();
            var typesByPriority = TocItemTypes.ToDictionary(key => key.TypeName, value => value.Priority);
            var titlesByType = TocItemTypes.ToDictionary(key => key.TypeName, value => value.Title);

            foreach (var typeName in typesByPriority.Keys)
            {
                if (!toc.ContainsKey(typeName)) continue;

                // Flatten all the sections into a single list of items
                var items = toc[typeName].SelectMany(e => e.Items).OrderBy(e => e.Titles.IndexTitle.ToLowerInvariant()).ToList();
                groups.Add(new TocTypeGroup(typeName, titlesByType[typeName], items));
            }
            
            return groups;
        }

        private Dictionary<string, DeclaredInAndItems> GroupByDeclaredIn(ApiReferenceEntity entity,
                                                                         List<ApiReferenceTocSection> sections,
                                                                         Dictionary<string, DeclaredInAndItems> target)
        {
            foreach (var section in sections)
            {
                var key = entity.Uri.Href;
                if (!string.IsNullOrWhiteSpace(section.DeclaredIn?.Uri?.Href))
                {
                    key = section.DeclaredIn.Uri.Href;
                }

                if (!target.ContainsKey(key))
                {
                    var declaredIn = section.DeclaredIn != null && section.DeclaredIn.Id.Id != entity.Id.Id
                                       ? section.DeclaredIn
                                       : null;
                    target.Add(key, new DeclaredInAndItems(declaredIn));
                }

                foreach (var item in section.Items)
                {
                    target[key].Items.Add(item);
                }
            }

            return target;
        }

        private List<string> FlattenInheritanceTree(ApiReferenceInheritance.ApiReferenceInheritanceNode node, List<string> target)
        {
            if (node == null) return target;

            target.Add(node.Uri);
            foreach (var child in node.Children)
            {
                target = FlattenInheritanceTree(child, target);
            }

            return target;
        }

        private class DeclaredInAndItems
        {
            public ApiReferenceTocSection.ApiReferenceTocSectionDeclaredIn DeclaredIn { get; }
            public List<ApiReferenceTocItem> Items { get; } = new List<ApiReferenceTocItem>();

            public DeclaredInAndItems(ApiReferenceTocSection.ApiReferenceTocSectionDeclaredIn declaredIn)
            {
                DeclaredIn = declaredIn;
            }
        }

        private class TocTypeDefinition
        {
            public string TypeName { get; }
            public string Title { get; }
            public int Priority { get; }

            public TocTypeDefinition(string typeName, string title, int priority)
            {
                TypeName = typeName;
                Title = title;
                Priority = priority;
            }
        }
    }
}