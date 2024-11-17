using SpotifyRunnerApp.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using System.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;
using static System.Net.WebRequestMethods;
using Microsoft.AspNetCore.Http.Headers;
using System.Net.Http;

namespace SpotifyRunnerApp.Services
{
    public class SpotifyAPIService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public SpotifyAPIService(HttpClient httpClient, IConfiguration configuration)
        {
            _configuration = configuration;
            _httpClient = httpClient;
        }

        //[ApiController]
        //[Route("api/[controller]")]
        //public class SpotifyController : ControllerBase
        //{
        //    private readonly HttpClient _httpClient;

        //    public SpotifyController(HttpClient httpClient)
        //    {
        //        _httpClient = httpClient;
        //    }
        //}


        public async Task<List<Playlist>> GetUserPlaylists(string accessToken)
        {
            string url = "https://api.spotify.com/v1/me/playlists";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to retreive playlists: {response.ReasonPhrase}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            //System.Diagnostics.Debug.WriteLine("Playlists JSON Response: " + jsonResponse);

            var playlistResponse = JsonSerializer.Deserialize<PlaylistResponse>(jsonResponse);
            
            if (playlistResponse.Playlists.Count == 0)
            {
                return new List<Playlist>();
            }

            return playlistResponse?.Playlists;
        }


        public async Task<Playlist> GetPlaylist(string accessToken, string playlistId)
        {
            string url = "https://api.spotify.com/v1/playlists/" + playlistId;

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to retreive playlist: {response.ReasonPhrase}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
           // Console.WriteLine("Playlists Response: " + jsonResponse);

            var playlist = JsonSerializer.Deserialize<Playlist>(jsonResponse);
            //Console.WriteLine(playlist);
            return playlist;
        }

        public async Task<String> QueueSongs(List<AudioFeature> tempos, string accessToken, float duration)
        {
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new Exception("No AccessToken provided");
            }
            //Keep tracks of the minutes queued so that we can exit once we reach the limit.
            float currentDuration = 0;
            //1000 milleseconds in a second
            float msToS = 1000;
            //60 seconds in a minute
            float sToMin = 60;
            // Iterate over each song in the list
            foreach (var song in tempos)
            {
                // Construct the URI with the song's id
                string uri = song.Uri;
                string url = $"https://api.spotify.com/v1/me/player/queue?uri={Uri.EscapeDataString(uri)}";

                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to add song {song.Id} to the queue: {response.ReasonPhrase}");
                }
                //convert the duration to minutes and add to currentDuration
                currentDuration += (song.MsDuration / msToS / sToMin);
                if (currentDuration > duration)
                {
                    return string.Format("{0:N2}", currentDuration);
                }
            }
            return string.Format("{0:N2}", currentDuration);
        }

        public async Task<QueueResponse> QueueDemoPlaylist(List<string> uris, string accessToken)
        {
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new Exception("No access token provided");
            }

            var results = new List<QueueResult>();

            foreach (var uri in uris)
            {
                string url = $"https://api.spotify.com/v1/me/player/queue?uri={Uri.EscapeDataString(uri)}";

                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.SendAsync(request);
                var result = new QueueResult
                {
                    Uri = uri,
                    Success = response.IsSuccessStatusCode,
                    ErrorMessage = response.IsSuccessStatusCode ? null : response.ReasonPhrase
                };
                results.Add(result);
            }

            return new QueueResponse
            {
                TotalQueued = results.Count(r => r.Success),
                Failed = results.Where(r => !r.Success).ToList()
            };
        }

        public async Task<List<string>> GetAllSavedTrackIds(string accessToken)
        {
            int limit = 50;
            int offset = 0;
            bool hasMoreTracks = true;
            var allSongIds = new List<string>();

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            while (hasMoreTracks)
            {
                var requestUrl = $"https://api.spotify.com/v1/me/tracks?limit={limit}&offset={offset}";
                var response = await httpClient.GetAsync(requestUrl);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception("Failed to retreive liked songs from Spotify");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var likedSongsData = JsonSerializer.Deserialize<LikedSongsResponse>(jsonResponse);

                foreach (var item in likedSongsData.Items)
                {
                    if (item.Track != null)
                    {
                        allSongIds.Add(item.Track.Id);
                    }
                }
                hasMoreTracks = likedSongsData.Items.Count == limit;
                offset += limit;
            }
            return allSongIds;

        }

        public async Task<List<AudioFeature>> GetTemposForTracks(List<string> trackIds, string accessToken)
        {
            var allAudioFeatures = new List<AudioFeature>();
            //max you can call at a time is 100.
            int batchSize = 100;

            for (int i = 0; i < trackIds.Count; i += batchSize)
            {
                var trackBatch = trackIds.Skip(i).Take(batchSize).ToList();
                string ids = string.Join(",", trackBatch);

                string url = $"https://api.spotify.com/v1/audio-features?ids={Uri.EscapeDataString(ids)}";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.SendAsync(request);
                //System.Diagnostics.Debug.WriteLine("Tempo Response: " + response);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to retreive audio features for batch starting at index {i}: {response.ReasonPhrase}");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                //System.Diagnostics.Debug.WriteLine("Audio Features JSON Response: " + jsonResponse);

                var audioFeaturesResponse = JsonSerializer.Deserialize<AudioFeaturesResponse>(jsonResponse);

                if (audioFeaturesResponse?.AudioFeatures != null)
                {
                    //var filteredFeatures = audioFeaturesResponse.AudioFeatures
                    //    .Where(feature => feature.Tempo >= 180 && feature.Tempo <= 200)
                    //    .ToList();

                    allAudioFeatures.AddRange(audioFeaturesResponse.AudioFeatures);
                }
            }
            return allAudioFeatures;
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
