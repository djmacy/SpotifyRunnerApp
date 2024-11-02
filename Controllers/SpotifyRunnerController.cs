using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Web;
using System.Net.Http.Headers;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration.UserSecrets;
using SpotifyRunnerApp.Models;

namespace spotifyRunnerApp.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class SpotifyRunnerController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ApplicationDbContext _dbContext;

        //private static string _accessToken;

        public SpotifyRunnerController(IConfiguration config, ApplicationDbContext dbContext)
        {
            _config = config;
            _dbContext = dbContext;
        }

        [HttpGet("testjson")]
        public IActionResult TestJson()
        {
            var data = new
            {
                message = "Hello, this is a test JSON response",
                timestamp = DateTime.Now
            };
            //System.Diagnostics.Debug.WriteLine(data);

            string jsonData = JsonSerializer.Serialize(data);
            //Console.WriteLine($"JSON Response: {jsonData}");

            return Ok(data);
        }

        [HttpGet("login")]
        public IActionResult Login()
        {
            string clientId = _config["Spotify:ClientId"];
            string redirectUri = _config["Spotify:RedirectUri"];
            string scope = _config["Spotify:Scope"]; 
            string state = GenerateRandomString(16);

            var queryParams = new Dictionary<string, string>
            {
                { "response_type", "code" },
                { "client_id", clientId },
                { "scope", scope },
                { "redirect_uri", redirectUri },
                { "state", state }
            };

            string queryString = QueryStringFromDictionary(queryParams); 
            string spotifyAuthUrl = $"https://accounts.spotify.com/authorize?{queryString}";

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

            // Get client ID and secret from config
            string clientId = _config["Spotify:ClientId"];
            string clientSecret = _config["Spotify:ClientSecret"];
            string redirectUri = _config["Spotify:RedirectUri"];

            // Prepare the token request
            using var httpClient = new HttpClient();
            var requestBody = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", redirectUri),
                new KeyValuePair<string, string>("grant_type", "authorization_code")
            });

            // Set up the request headers
            var clientCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", clientCredentials);

            // Send the token request
            var tokenResponse = await httpClient.PostAsync("https://accounts.spotify.com/api/token", requestBody);

            if (tokenResponse.IsSuccessStatusCode)
            {
                var jsonResponse = await tokenResponse.Content.ReadAsStringAsync();

                var tokenData = JsonSerializer.Deserialize<TokenResponse>(jsonResponse);
                var accessToken = tokenData.AccessToken;
                
                string userId = "";
                //Get the user id
                var userProfileResult = await GetUserProfile(tokenData.AccessToken);
                if (userProfileResult is OkObjectResult okResult)
                {
                    if (okResult.Value != null)
                    {
                        userId = okResult.Value as string;
                        var newUser = new SpotifyUser
                        {
                            Username = userId,
                            AccessToken = tokenData.AccessToken,
                            ExpiresIn = tokenData.ExpiresIn.ToString(),
                            RefreshToken = tokenData.RefreshToken
                        };

                        //Add user to db
                        await _dbContext.spotify_user.AddAsync(newUser);
                        await _dbContext.SaveChangesAsync();

                    } else
                    {
                        return BadRequest("User id is null");
                    }
                    
                } else
                {
                    return BadRequest("Failed to grab user Id");
                }
                return Ok(new { message = "Access token received", accessToken = accessToken, userId = userId });
            }
            else
            {
                return BadRequest("Failed to exchange code for token");
            }
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetUserProfile(string accessToken)
        {
            if (accessToken == null)
            {
                return BadRequest("No access token provided"); ;
            }
            // Assuming you have stored the access token after exchanging the code
            //string accessToken = _accessToken; // Replace this with the actual access token

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.GetAsync("https://api.spotify.com/v1/me");
            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var userData = JsonSerializer.Deserialize<UserProfile>(jsonResponse);
                if (userData != null)
                {
                    return Ok(userData.Id);
                } else
                {
                    return BadRequest("Failed to deserailzie user profile");
                }
              //  System.Diagnostics.Debug.WriteLine(userData);
                //return Ok(jsonResponse); // Return the user's profile information
            }
            else
            {
                return BadRequest("Failed to retrieve user profile");
            }
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
        public class TokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; }

            [JsonPropertyName("token_type")]
            public string TokenType { get; set; }

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }

            [JsonPropertyName("refresh_token")]
            public string RefreshToken { get; set; } 
        }

        public class UserProfile
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }
            [JsonPropertyName("display_name")]
            public string DisplayName { get; set; }
            [JsonPropertyName("email")]
            public string Email { get; set; }

        }
    }
}
