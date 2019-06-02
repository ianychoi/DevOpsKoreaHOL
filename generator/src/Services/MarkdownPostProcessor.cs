using System.Threading.Tasks;

namespace Builder.Services
{
    public abstract class MarkdownPostProcessor
    {
        public abstract Task<string> ProcessAsync(string html);
    }
}