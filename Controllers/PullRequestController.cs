using GitHubPrBot.Models;
using GitHubPrBot.Services;
using Microsoft.AspNetCore.Mvc;

namespace GitHubPrBot.Controllers
{
    [ApiController]
    [Route("api/pull-request")]
    public class PullRequestController : ControllerBase
    {
        private readonly IGitHubService _gitHubService;

        public PullRequestController(IGitHubService gitHubService)
        {
            _gitHubService = gitHubService;
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreatePrRequest request)
        {
            try
            {
                request.BranchName ??= $"chatbot-{Guid.NewGuid()}";
                await _gitHubService.CreatePullRequestAsync(request);
                return Ok("Pull Request created successfully");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}
