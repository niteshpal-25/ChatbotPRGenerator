namespace GitHubPrBot.Models
{
    public class CreatePrRequest
    {
        public string BranchName { get; set; }
        public string FilePath { get; set; }
        public string FileContent { get; set; }
        public string CommitMessage { get; set; }
        public string PrTitle { get; set; }
        public string PrBody { get; set; }
    }
}
