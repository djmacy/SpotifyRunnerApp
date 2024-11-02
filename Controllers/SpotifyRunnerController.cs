using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Web;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
using SpotifyRunnerApp.Services;
using SpotifyRunnerApp.Models;

namespace spotifyRunnerApp.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SpotifyRunnerController : ControllerBase
    {
        //Grab the configs so that we can use them for spotify api calls. 
        private readonly IConfiguration _config;
        //Get the psql database services 
        private readonly SpotifyUserService _userService;

        //constructor for controller to make sure we have the service and configs
        public SpotifyRunnerController(IConfiguration config, SpotifyUserService userService)
        {
            _config = config;
            _userService = userService;
        }

        [HttpGet("testjson")]
        public IActionResult TestJson()
        {
            var data = new
            {
                message = "Hello, this is a test JSON response",
                timestamp = DateTime.Now
            };
            string jsonData = JsonSerializer.Serialize(data);
            return Ok(data);
        }

        // spotifyRunner/login endpoint which will redirect us to the spotify auth url. This will allows us to get access or permission to see users spotify information by giving us the authentication code which will
        //be exchanged in the /callback endpoint for the auth token. With the token we can call the spotify api passing it as a header in our calls.
        [HttpGet("login")]
        public IActionResult Login()
        {
            string clientId = GetConfigValue("Spotify:ClientId");
            string redirectUri = GetConfigValue("Spotify:RedirectUri");
            string scope = GetConfigValue("Spotify:Scope");
            string state = GenerateRandomString(16);

            string spotifyAuthUrl = BuildSpotifyAuthUrl(clientId, redirectUri, scope, state);

            return Redirect(spotifyAuthUrl);
        }


        [HttpGet("callback")]
        public async Task<IActionResult> Callback(string code, string state)
        {

            // Check state to mitigate CSRF attacks
            if (state == null)
            {
                return BadRequest("State mismatch error");
            }

            // Retrieve configuration values
            var clientId = GetConfigValue("Spotify:ClientId");
            var clientSecret = GetConfigValue("Spotify:ClientSecret");
            var redirectUri = GetConfigValue("Spotify:RedirectUri");

            var tokenResponse = await ExchangeCodeForToken(code, clientId, clientSecret, redirectUri);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                return BadRequest("Failed to exchange code for token");
            }

            var jsonResponse = await tokenResponse.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<TokenResponse>(jsonResponse);

            var userId = await GetUserIdFromToken(tokenData.AccessToken);

            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("Failed to retrieve user id");
            }

            // Upsert user information
            await _userService.UpsertUser(userId, tokenData.AccessToken, tokenData.ExpiresIn, tokenData.RefreshToken);

            return Ok(new { message = "Access token received", accessToken = tokenData.AccessToken, userId });
        }

       
        /**
         * Come back to this by getting db value?  
         **/
        //[HttpGet("tracks")]
        //public async Task<IActionResult> getUserTopSongs()
        //{
        //    string accessToken = _accessToken;
        //    using var httpClient = new HttpClient();
        //    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        //    var response = await httpClient.GetAsync("https://api.spotify.com/v1/me/top/tracks");
        //    if (response.IsSuccessStatusCode)
        //    {
        //        var jsonResponse = await response.Content.ReadAsStringAsync();
        //        return Ok(jsonResponse);
        //    }
        //    else
        //    {
        //        return BadRequest("Failed to retrieve user tracks");
        //    }
        //}

        //In postman after loggin in through the spotifyRunner web app https://localhost:44336/spotifyrunner/add-to-queue?trackUri=spotify:track:4iV5W9uYEdYUVa79Axb7Rh
        //[HttpPost("add-to-queue")]
        //public async Task<IActionResult> AddToQueue([FromQuery] string trackUri)
        //{
        //    string accessToken = _accessToken;
        //    using var httpClient = new HttpClient();
        //    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        //    var url = $"https://api.spotify.com/v1/me/player/queue?uri={Uri.EscapeDataString(trackUri)}";
        //    var response = await httpClient.PostAsync(url, null);

        //    if (response.IsSuccessStatusCode)
        //    {
        //        return Ok(new { message = "Check your phone bozo" });
        //    }
        //    else
        //    {
        //        var errorResponse = await response.Content.ReadAsStringAsync();
        //        return BadRequest(new { message = "Failed to add track to queue", details = errorResponse });
        //    }
        //}

        private string GetConfigValue(string key) => _config[key];

        private async Task<UserProfile> GetUserProfile(string accessToken)
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

        private async Task<HttpResponseMessage> ExchangeCodeForToken(string code, string clientId, string clientSecret, string redirectUri)
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

        private async Task<string> GetUserIdFromToken(string accessToken)
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

        private string BuildSpotifyAuthUrl(string clientId, string redirectUri, string scope, string state)
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

        private static string GenerateRandomString(int length) // Fixed the method name here
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                                         .Select(s => s[random.Next(s.Length)]) // Fixed case for Length
                                         .ToArray()); // Changed to ToArray() for correct conversion
        }

        // Model for deserializing token response
        //public class TokenResponse
        //{
        //    [JsonPropertyName("access_token")]
        //    public string AccessToken { get; set; }

        //    [JsonPropertyName("token_type")]
        //    public string TokenType { get; set; }

        //    [JsonPropertyName("expires_in")]
        //    public int ExpiresIn { get; set; }

        //    [JsonPropertyName("refresh_token")]
        //    public string RefreshToken { get; set; } 
        //}

        //public class UserProfile
        //{
        //    [JsonPropertyName("id")]
        //    public string Id { get; set; }
        //    [JsonPropertyName("display_name")]
        //    public string DisplayName { get; set; }
        //    [JsonPropertyName("email")]
        //    public string Email { get; set; }

        //}
    }
}
