namespace Builder.Services
{
    public static class HtmlHelpers
    {
        public static string EscapeHtml(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            return text.Replace("\"", "&quot;")
                       .Replace("&", "&amp;")
                       .Replace("<", "&lt;")
                       .Replace(">", "&gt;");
        }

        public static string UnescapeHtml(string html) {
            if (string.IsNullOrWhiteSpace(html))
            {
                return html;
            }

            return html.Replace("&gt;", ">")
                       .Replace("&lt;", "<")
                       .Replace("&amp;", "&")
                       .Replace("&quot;", "\"");
        }
    }
}