namespace Builder.Models
{
    public class ArticleQualityReport
    {
        public string Path { get; }
        public int NumberOfLines { get; }

        public ArticleQualityReport(string path, int numberOfLines)
        {
            Path = path;
            NumberOfLines = numberOfLines;
        }
    }
}