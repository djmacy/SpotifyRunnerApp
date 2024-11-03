using SpotifyRunnerApp.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using System.Web;
using Microsoft.EntityFrameworkCore;

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

        public async Task<List<AudioFeature>> GetTemposForTracks(List<string> trackIds, string accessToken)
        {
            System.Diagnostics.Debug.WriteLine("List of songs: " + trackIds[0]);
            string ids = string.Join(",", trackIds);

            string url = $"https://api.spotify.com/v1/audio-features?ids={Uri.EscapeDataString(ids)}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);
            System.Diagnostics.Debug.WriteLine("Tempo Response: " + response);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine("Audio Features: " + jsonResponse);
            var audioFeaturesResponse = JsonSerializer.Deserialize<AudioFeaturesResponse>(jsonResponse);
            //System.Diagnostics.Debug.WriteLine("Audio Features: " + audioFeaturesResponse);


            return audioFeaturesResponse.AudioFeatures;
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

        public async Task<TokenResponse> RefreshAccessToken(string refreshToken)
        {

            // Define the endpoint and the payload
            const string url = "https://accounts.spotify.com/api/token";
            //var clientId = GetConfigValue("Spotify:ClientId"); // Assuming you have a method to retrieve config values
           
            var payload = new
            {
                grant_type = "refresh_token",
                refresh_token = refreshToken,
                //client_id = clientId
            };

            using var httpClient = new HttpClient();
            var response = await httpClient.PostAsync(url, new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", payload.grant_type),
                new KeyValuePair<string, string>("refresh_token", payload.refresh_token),
                //new KeyValuePair<string, string>("client_id", payload.client_id),
            }));

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed to refresh access token");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<TokenResponse>(jsonResponse);

            // Here you can upsert the new token data into the database
            //await UpsertUser(user.Username, tokenData.AccessToken, tokenData.ExpiresIn, tokenData.RefreshToken);

            return tokenData; // Return the token data for further use if needed
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
