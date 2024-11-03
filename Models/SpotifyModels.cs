namespace SpotifyRunnerApp.Models
{
    using System.Numerics;
    using System.Text.Json.Serialization;

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

    // Models/UserProfile.cs
    public class UserProfile
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }
    }

    // Models/LikedSongsResponse.cs
    public class LikedSongsResponse
    {
        [JsonPropertyName("next")]

        public string Next { get; set; }

        [JsonPropertyName("offset")]

        public int Offset { get; set; }

        [JsonPropertyName("items")]
        public List<SongItem> Items { get; set; }
    }

    public class SongItem
    {
        [JsonPropertyName("track")]
        public Track Track { get; set; }
    }

    public class Track
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        [JsonPropertyName("artists")]
        public List<Artist> Artists { get; set; }
    }

    public class Artist
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }
    }

    // Models/AudioFeaturesResponse.cs
    public class AudioFeaturesResponse
    {
        [JsonPropertyName("audio_features")]
        public List<AudioFeature> AudioFeatures { get; set; }
    }

    public class AudioFeature
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("tempo")]
        public float Tempo { get; set; }
    }

}