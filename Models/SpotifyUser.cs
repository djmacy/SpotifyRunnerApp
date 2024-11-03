using System.ComponentModel.DataAnnotations.Schema;

namespace SpotifyRunnerApp.Models
{
    [Table("spotify_user")]
    public class SpotifyUser
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string AccessToken { get; set; }
        public int ExpiresIn { get; set; }
        public string RefreshToken { get; set; }
        public long CreatedAt { get; set; }
    }
}
