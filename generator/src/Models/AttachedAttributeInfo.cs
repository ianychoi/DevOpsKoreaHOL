namespace Builder.Models
{
    public class AttachedAttributeInfo
    {
        public string AttributeType { get; }
        public string AttachedByType { get; }
        public string AttachedByHref { get; }
        public string AttachedToType { get; }
        public string AttachedToHref { get; }
        public string Name { get; }
        public string FullName { get; }

        public AttachedAttributeInfo(string attributeType,
                                     string attachedByType,
                                     string attachedByHref,
                                     string attachedToType,
                                     string attachedToHref,
                                     string name,
                                     string fullName)
        {
            AttributeType = attributeType;
            AttachedByType = attachedByType;
            AttachedByHref = attachedByHref;
            AttachedToType = attachedToType;
            AttachedToHref = attachedToHref;
            Name = name;
            FullName = fullName;
        }
    }
}