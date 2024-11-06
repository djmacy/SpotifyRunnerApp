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
        // Grab the configs so that we can use them for spotify api calls. 
        private readonly IConfiguration _config;
        // Get the psql database services 
        private readonly SpotifyUserService _userService;
        private readonly SpotifyAPIService _spotifyAPIService;

        // constructor for controller to make sure we have the service and configs. Dependency Injection
        public SpotifyRunnerController(IConfiguration config, SpotifyUserService userService, SpotifyAPIService spotifyAPIService)
        {
            _config = config;
            _userService = userService;
            _spotifyAPIService = spotifyAPIService;
        }

        [HttpGet("testjson")]
        public IActionResult TestJson()
        {
            System.Diagnostics.Debug.WriteLine("Test endpoint hit");
            var data = new
            {
                message = "Hello, this is a test JSON response",
                timestamp = DateTime.Now
            };
            string jsonData = JsonSerializer.Serialize(data);
            return Ok(data);
        }

        // spotifyRunner/login endpoint which will redirect us to the spotify auth url. This will allows us to get access or permission to see users spotify information by giving us the authentication code which will
        // be exchanged in the /callback endpoint for the auth token. With the token we can call the spotify api passing it as a header in our calls.
        [HttpGet("login")]
        public IActionResult Login()
        {
            string clientId = GetConfigValue("Spotify:ClientId");
            //Is used to redirect the user back to our site. Spotify has to know what this is as well and is set up in dev dashboard for spotify.
            string redirectUri = GetConfigValue("Spotify:RedirectUri");
            //Different scopes give us different permissions. See spotify doc for more info.
            string scope = GetConfigValue("Spotify:Scope");
            // Generate a random string to help protect against csrf attacks. Will need to verify that we get this back when we redirect to callback.
            string state = GenerateRandomString(16);
            // store the state in a session so we can check later.
            HttpContext.Session.SetString("OAuthState", state);
            // URL used to send the user to the Spotify Login screen and show perm we need in order to run our endpoints.
            string spotifyAuthUrl = _spotifyAPIService.BuildSpotifyAuthUrl(clientId, redirectUri, scope, state);
            // This redirection will take us to the spotify login. If user accepts, spotify will then redirect them back to our site using redirect URI
            // defined above. If they deny then it will also redirect but with deny response. 
            return Redirect(spotifyAuthUrl);
        }

        // As part of the redirection above spotify redirects the user back to the url you specified as redirect_uri during the authorization request
        // In ASP.NET parameters in query string are automatically bound to method parameters so the code and state are coming straight from Spotify
        // response. This endpoint will grab the auth token for us to be able to make api calls to spotify regarding our users music.
        [HttpGet("callback")]
        public async Task<IActionResult> Callback(string code, string state)
        {
            // Check the session variable to make sure the states match.
            var storedState = HttpContext.Session.GetString("OAuthState");
            if (string.IsNullOrEmpty(state) || state != storedState)
            {
                return BadRequest("State mismatch error");
            }
            //get rid of it so that we can optimize memory. We wont be needing this anymore.
            HttpContext.Session.Remove("OAuthState");

            // Retrieve configuration values.
            var clientId = GetConfigValue("Spotify:ClientId");
            var clientSecret = GetConfigValue("Spotify:ClientSecret");
            var redirectUri = GetConfigValue("Spotify:RedirectUri");

            // Retrieves the authentication
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
            //Store this in order to be able to make calls to db with user id whenever we will need it
            HttpContext.Session.SetString("UserId", userId);
            // Upsert user information
            await _userService.UpsertUser(userId, tokenData.AccessToken, tokenData.ExpiresIn, tokenData.RefreshToken);

            return Ok(new { message = "Access token received", accessToken = tokenData.AccessToken, userId });
        }

        [HttpGet("myLikedSongs")]
        public async Task<IActionResult> getLikedSongs(int limit = 50, int offset = 0)
        {
            string username = HttpContext.Session.GetString("UserId");
            if (username == null)
            {
                return BadRequest("No username provided");
            }
            string accessToken = await _userService.GetAccessTokenByUsername(username);
            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized("Access Token missing or invalid");
            }

            var allSongIds = await _spotifyAPIService.GetAllSavedTrackIds(accessToken);
            //System.Diagnostics.Debug.Write("All songs with URI: " + allSongIds);
            if (allSongIds.Count == 0)
            {
                return Ok("No saved tracks found.");
            }

            // Retrieve tempos for all songs
            var tempos = await _spotifyAPIService.GetTemposForTracks(allSongIds, accessToken);
            var lengthQueued = await _spotifyAPIService.QueueSongs(tempos, accessToken, 10);
            System.Diagnostics.Debug.WriteLine("Duration: " + lengthQueued);
            return Ok(tempos);
        }

        [HttpGet("playlists")]
        public async Task<IActionResult> GetPlaylists()
        {
            System.Diagnostics.Debug.WriteLine("GetPlaylists endpoint was called");
            string username = HttpContext.Session.GetString("UserId");
            if (String.IsNullOrEmpty(username))
            {
                return BadRequest("No username provided");
            }
            string accessToken = await _userService.GetAccessTokenByUsername(username);
            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized("Access Token missing or invalid");
            }
            var playlists = await _spotifyAPIService.GetUserPlaylists(accessToken);
            return Ok(playlists);
        }

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
