using SpotifyRunnerApp.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using System.Web;

namespace SpotifyRunnerApp.Services
{
    public class SpotifyAPIService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public SpotifyAPIService(IConfiguration configuration)
        {
            _configuration = configuration;
            _httpClient = new HttpClient();
        }

        public async Task<UserProfile> GetUserProfile(string accessToken)
        {
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new ArgumentException("No access token provided", nameof(accessToken));
            }

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.GetAsync("https://api.spotify.com/v1/me");

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var userData = JsonSerializer.Deserialize<UserProfile>(jsonResponse);

                if (userData != null)
                {
                    return userData;
                }
                else
                {
                    throw new InvalidOperationException("Failed to deserialize user profile");
                }
            }
            else
            {
                throw new InvalidOperationException("Failed to retrieve user profile");
            }
        }

        public async Task<HttpResponseMessage> ExchangeCodeForToken(string code, string clientId, string clientSecret, string redirectUri)
        {
            using var httpClient = new HttpClient();
            var requestBody = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", redirectUri),
                new KeyValuePair<string, string>("grant_type", "authorization_code")
             });

            var clientCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", clientCredentials);

            return await httpClient.PostAsync("https://accounts.spotify.com/api/token", requestBody);
        }

        public async Task<string> GetUserIdFromToken(string accessToken)
        {
            try
            {
                var userProfile = await GetUserProfile(accessToken);
                return userProfile?.Id; // Assuming UserProfile has an Id property
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as needed
                return null; // Or throw again, or handle error accordingly
            }
        }

        public string BuildSpotifyAuthUrl(string clientId, string redirectUri, string scope, string state)
        {
            var queryParams = new Dictionary<string, string>
            {
                { "response_type", "code" },
                { "client_id", clientId },
                { "scope", scope },
                { "redirect_uri", redirectUri },
                { "state", state }
            };

            string queryString = QueryStringFromDictionary(queryParams);
            return $"https://accounts.spotify.com/authorize?{queryString}";
        }
        private static string QueryStringFromDictionary(Dictionary<string, string> queryParams)
        {
            return string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={HttpUtility.UrlEncode(kvp.Value)}"));
        }
    }
}
