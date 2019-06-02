using System;

namespace Builder.Models
{
    public interface IApiJsonDocument
    {
        DateTime SourceFileLastModifiedAt { get; set; }
    }
}