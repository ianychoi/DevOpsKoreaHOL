namespace Builder.Models
{
    public class ApiReferenceInterface
    {
        public ApiReferenceId Id { get; set; }
        public ApiReferenceUri Uri { get; set; }
        public ApiReferenceTitle Titles { get; set; }
        public ApiReferenceComment Comment { get; set; }
    }
}