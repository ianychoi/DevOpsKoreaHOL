using System;
using System.Collections.Generic;
using System.Linq;
using Builder.Models;

namespace Builder.Services
{
    public static class ApiRenderingHelper
    {
        private static readonly HashSet<string> AttachedUxAttributeIds = new HashSet<string>(new[]
        {
            ""
        });

        private static readonly HashSet<string> ImplicitlyAdvancedTypes = new HashSet<string>(new[]
        {
            "class",
            "delegate",
            "enum",
            "interface",
            "struct",
            "constructor",
            "property",
            "method",
            "event",
            "field",
            "cast",
            "operator",
            "literal",
            "swizzlertype"
        });

        public static AttachedAttributeInfo GetAttachedAttributeInfo(ApiReferenceId id,
                                                                     ApiReferenceTitle title,
                                                                     List<ApiReferenceParameter> parameters,
                                                                     List<ApiReferenceAttribute> attributes,
                                                                     Dictionary<string, ApiReferenceEntity> entityCache)
        {
            var uxAttribute = attributes?.FirstOrDefault(e => e.Uri.Href.StartsWith("uno/ux/uxattached") && e.Parameters.Count > 0);
            if (id.Type != "Method" || parameters?.Count < 2 || uxAttribute == null) return null;

            // The attribute type is determined by the attribute name
            var attributeType = "";
            if (uxAttribute.Uri.Href.Contains("uxattachedproperty")) attributeType = "property";
            else if (uxAttribute.Uri.Href.Contains("uxattachedevent")) attributeType = "event";
            else if (uxAttribute.Uri.Href.Contains("uxattachedmethod")) attributeType = "method";

            if (!entityCache.ContainsKey(id.ParentId))
            {
                throw new ArgumentException($"Found attached UX attribute {id.Id} where the parent id {id.ParentId} was not found");
            }
            var parent = entityCache[id.ParentId];
            var attachedByType = parent.Titles.IndexTitle;
            var attachedByHref = parent.Uri.Href;

            // To identify what type the member is attached to, we use the type of the first parameter
            // of the method
            var attachedToType = parameters[0].Title;
            var attachedToHref = parameters[0].Href;

            // To find the names of the attribute, we use the first parameter of the attribute declaration,
            // and use the last segment of that as the base name.
            var fullName = uxAttribute.Parameters[0];
            var name = fullName.Split('.').Last();

            return new AttachedAttributeInfo(attributeType: attributeType,
                                             attachedByType: attachedByType,
                                             attachedByHref: attachedByHref,
                                             attachedToType: attachedToType,
                                             attachedToHref: attachedToHref,
                                             name: name,
                                             fullName: fullName);
        }

        public static string GetTitle(ApiReferenceId id,
                                      ApiReferenceTitle title,
                                      ApiReferenceComment comment,
                                      List<ApiReferenceParameter> parameters,
                                      List<ApiReferenceAttribute> attributes,
                                      Dictionary<string, ApiReferenceEntity> entityCache,
                                      bool isIndex)
        {
            var attachedAttribute = GetAttachedAttributeInfo(id, title, parameters, attributes, entityCache);
            if (!isIndex && attachedAttribute != null)
            {
                return $"{attachedAttribute.Name} attached {attachedAttribute.AttributeType} on {attachedAttribute.AttachedToType}";
            }

            // Try to capture the type name from the fully qualified title.
            // This is basically stripping away any arguments, then taking the second last segment after
            // splitting on period so that:
            //   > Fuse.Elements.Element.Equals(Fuse.Elements.Element other)
            // turns into:
            //   > Element
            string typeName = title.FullyQualifiedIndexTitle;
            if (typeName.Contains("("))
            {
                typeName = typeName.Substring(0, typeName.IndexOf("("));
            }
            if (typeName.Contains("."))
            {
                var parts = typeName.Split('.');
                typeName = parts[parts.Length - 2];
            }

            // JavaScript methods use the ScriptMethod comment property to determine title
            if (id.Type == "JsMethod")
            {
                if (comment?.Attributes?.ScriptMethod == null)
                {
                    throw new ArgumentException($"Found JsMethod without script method comment, unable to generate title: {id.Id}");
                }

                var name = comment.Attributes.ScriptMethod.Name + "(" + string.Join(", ", comment.Attributes.ScriptMethod.Parameters) + ")";
                if (!isIndex)
                {
                    name =  typeName + "." + name + " Method (JS)";
                }
                return name;
            }

            // JavaScript modules use ScriptModule comment property to determine title
            if (id.Type == "JsModule")
            {
                if (string.IsNullOrWhiteSpace(comment?.Attributes?.ScriptModule))
                {
                    throw new ArgumentException($"Found JsModule without script module comment, unable to generate title: {id.Id}");
                }

                var name = comment.Attributes.ScriptModule;
                if (!isIndex)
                {
                    name = name + " Module (JS)";
                }
                return name;
            }

            // JavaScript properties use the ScriptProperty comment to determine title
            if (id.Type == "JsProperty")
            {
                if (string.IsNullOrWhiteSpace(comment?.Attributes?.ScriptProperty))
                {
                    throw new ArgumentException($"Found JsProperty without script property comment, unable to generate title: {id.Id}");
                }

                var name = comment.Attributes.ScriptProperty;
                if (!isIndex)
                {
                    name = typeName + "." + name + " Property (JS)";
                }
                return name;
            }

            // JavaScript events uses the ScriptEvent comment to determine title
            if (id.Type == "JsEvent")
            {
                if (string.IsNullOrWhiteSpace(comment?.Attributes?.ScriptEvent))
                {
                    throw new ArgumentException($"Found JsEvent without script event comment, unable to generate title: {id.Id}");
                }

                var name = comment.Attributes.ScriptEvent;
                if (!isIndex)
                {
                    name = typeName + "." + name + " Event (JS)";
                }
                return name;
            }

            // Constructors should be named "<type> Constructor" in index titles for clarity
            if (id.Type == "Constructor" && isIndex)
            {
                return title.IndexTitle + " Constructor";
            }

            // Attached attributes should have their prefix removed so it only lists the name of the actual attribute
            if (isIndex && id.Type.StartsWith("AttachedUx") && title.IndexTitle.Contains("."))
            {
                return title.IndexTitle.Split('.')[1];
            }

            // Fall back to the title defined on the object
            return isIndex ? title.IndexTitle : title.PageTitle;
        }

        public static bool HasAdvancedItems(this Dictionary<string, List<ApiReferenceTocSection>> toc, ApiReferenceId containingPageId)
        {
            foreach (var pair in toc)
            {
                foreach (var section in pair.Value)
                {
                    foreach (var item in section.Items)
                    {
                        if (item.IsAdvanced(containingPageId))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public static bool HasOnlyAdvancedItems(this Dictionary<string, List<ApiReferenceTocSection>> toc, ApiReferenceId containingPageId)
        {
            foreach (var pair in toc)
            {
                foreach (var section in pair.Value)
                {
                    foreach (var item in section.Items)
                    {
                        if (!item.IsAdvanced(containingPageId))
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        public static bool IsAdvanced(this ApiReferenceTocItem item, ApiReferenceId parentId)
        {
            if (ImplicitlyAdvancedTypes.Contains(item.Id.Type.ToLowerInvariant()))
            {
                return true;
            }

            if ((item.Comment?.Attributes?.Advanced ?? false))
            {
                return true;
            }

            // Only Js* items are non-advanced for JS module pages
            if (parentId?.Type == "JsModule")
            {
                return !item.Id.Type.StartsWith("Js");
            }

            return false;
        }
    }
}