using GitHubPrBot.Models;

namespace GitHubPrBot.Services
{
    public interface IGitHubService
    {
        Task CreatePullRequestAsync(CreatePrRequest request);
    }
}
