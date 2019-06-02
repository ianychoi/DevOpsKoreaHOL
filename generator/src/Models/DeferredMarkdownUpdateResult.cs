using Builder.Services;

namespace Builder.Models
{
    public class DeferredMarkdownUpdateResult : Renderer
    {
        public string Html { get; }

        public DeferredMarkdownUpdateResult(string html)
        {
            Html = html;
        }
    }
}
