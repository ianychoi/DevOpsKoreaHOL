using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Builder.Services
{
    public class LinkCollector
    {
        private static readonly Regex HrefValuePattern = new Regex(@"<a\s+(?:[^>]*?\s+)?href=""([^""]*)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public List<string> GetLinksFrom(string input)
        {
            var result = new List<string>();
            var links = HrefValuePattern.Matches(input);
            foreach (Match match in links)
            {
                result.Add(match.Groups[1].Value);
            }
            return result;
        }
    }
}