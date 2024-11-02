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
        private readonly SpotifyAPIService _spotifyAPIService;

        //constructor for controller to make sure we have the service and configs
        public SpotifyRunnerController(IConfiguration config, SpotifyUserService userService, SpotifyAPIService spotifyAPIService)
        {
            _config = config;
            _userService = userService;
            _spotifyAPIService = spotifyAPIService;
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
            //Generate a random string to help protect against csrf attacks. Will need to verify that we get this back when we redirect to callback
            string state = GenerateRandomString(16);
            //store the state in a session so we can check later
            HttpContext.Session.SetString("OAuthState", state);
            string spotifyAuthUrl = _spotifyAPIService.BuildSpotifyAuthUrl(clientId, redirectUri, scope, state);

            return Redirect(spotifyAuthUrl);
        }


        [HttpGet("callback")]
        public async Task<IActionResult> Callback(string code, string state)
        {
            var storedState = HttpContext.Session.GetString("OAuthState");
            // Check state to mitigate CSRF attacks
            if (string.IsNullOrEmpty(state) || state != storedState)
            {
                return BadRequest("State mismatch error");
            }

            // Retrieve configuration values
            var clientId = GetConfigValue("Spotify:ClientId");
            var clientSecret = GetConfigValue("Spotify:ClientSecret");
            var redirectUri = GetConfigValue("Spotify:RedirectUri");

            var tokenResponse = await _spotifyAPIService.ExchangeCodeForToken(code, clientId, clientSecret, redirectUri);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                return BadRequest("Failed to exchange code for token");
            }

            var jsonResponse = await tokenResponse.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<TokenResponse>(jsonResponse);

            var userId = await _spotifyAPIService.GetUserIdFromToken(tokenData.AccessToken);

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

        private static string GenerateRandomString(int length) // Fixed the method name here
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                                         .Select(s => s[random.Next(s.Length)]) // Fixed case for Length
                                         .ToArray()); // Changed to ToArray() for correct conversion
        }
    }
}
