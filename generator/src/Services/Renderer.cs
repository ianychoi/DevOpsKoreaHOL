using System;

namespace Builder.Services
{
    public abstract class Renderer
    {
        protected string EscapeHtml(string text)
        {
            return HtmlHelpers.EscapeHtml(text);
        }

        protected int CountNumberOfLines(string text)
        {
            return text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).Length;
        }
    }
}