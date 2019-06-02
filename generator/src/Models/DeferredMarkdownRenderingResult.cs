using System;

namespace Builder.Models
{
    public class DeferredMarkdownRenderingResult
    {
        public Guid Id { get; }
        public string Html { get; }

        public DeferredMarkdownRenderingResult(Guid id, string html)
        {
            Id = id;
            Html = html;
        }
    }
}