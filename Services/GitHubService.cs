using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GitHubPrBot.Models;

namespace GitHubPrBot.Services
{
    public class GitHubService : IGitHubService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;

        public GitHubService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _config = config;

            _http.DefaultRequestHeaders.UserAgent.ParseAdd("ChatBot");
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _config["GitHub:Token"]);
            _http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        }

        private string Owner => _config["GitHub:Owner"];
        private string Repo => _config["GitHub:Repo"];
        private string BaseBranch => _config["GitHub:BaseBranch"];

        public async Task CreatePullRequestAsync(CreatePrRequest request)
        {
            try
            {
                await EnsureBranchExists(request.BranchName);
                await CommitFile(request);
                await OpenPullRequest(request);
            }
            catch (Exception ex)
            {
                throw new Exception($"GitHub PR creation failed: {ex.Message}", ex);
            }
        }

        private async Task EnsureBranchExists(string branch)
        {
            // Check if branch exists
            var url = $"https://api.github.com/repos/{Owner}/{Repo}/git/ref/heads/{branch}";
            var response = await _http.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Branch {branch} already exists.");
                return; // Branch exists
            }

            // Branch doesn't exist → create it from base branch
            var baseSha = await GetBaseBranchSha();
            await CreateBranch(branch, baseSha);
        }

        private async Task<string> GetBaseBranchSha()
        {
            var url = $"https://api.github.com/repos/{Owner}/{Repo}/git/ref/heads/{BaseBranch}";
            var response = await _http.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to get base branch SHA: {response.StatusCode} - {body}");

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("object").GetProperty("sha").GetString();
        }

        private async Task CreateBranch(string branch, string sha)
        {
            var payload = new
            {
                @ref = $"refs/heads/{branch}",
                sha
            };

            var response = await _http.PostAsync(
                $"https://api.github.com/repos/{Owner}/{Repo}/git/refs",
                CreateJsonContent(payload));

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to create branch '{branch}': {response.StatusCode} - {body}");
        }

        private async Task<string> GetFileShaIfExists(string filePath, string branch)
        {
            var url = $"https://api.github.com/repos/{Owner}/{Repo}/contents/{filePath}?ref={branch}";
            var response = await _http.GetAsync(url);

            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("sha").GetString();
        }

        private async Task CommitFile(CreatePrRequest request)
        {
            var sha = await GetFileShaIfExists(request.FilePath, request.BranchName);

            var payload = new
            {
                message = request.CommitMessage,
                content = Convert.ToBase64String(Encoding.UTF8.GetBytes(request.FileContent)),
                branch = request.BranchName,
                sha // include only if file exists
            };

            var response = await _http.PutAsync(
                $"https://api.github.com/repos/{Owner}/{Repo}/contents/{request.FilePath}",
                CreateJsonContent(payload));

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to commit file '{request.FilePath}': {response.StatusCode} - {body}");
        }

        private async Task OpenPullRequest(CreatePrRequest request)
        {
            var payload = new
            {
                title = request.PrTitle,
                body = request.PrBody,
                head = $"{Owner}:{request.BranchName}",
                @base = BaseBranch
            };

            var response = await _http.PostAsync(
                $"https://api.github.com/repos/{Owner}/{Repo}/pulls",
                CreateJsonContent(payload));

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to create pull request: {response.StatusCode} - {body}");

            using var doc = JsonDocument.Parse(body);
            var prUrl = doc.RootElement.GetProperty("html_url").GetString();
            Console.WriteLine($"Pull Request created: {prUrl}");
        }

        private static StringContent CreateJsonContent(object data)
        {
            return new StringContent(
                JsonSerializer.Serialize(data),
                Encoding.UTF8,
                "application/json");
        }
    }
}
