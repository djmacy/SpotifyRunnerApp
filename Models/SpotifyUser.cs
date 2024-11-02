namespace SpotifyRunnerApp.Models
{
    public class SpotifyUser
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string AccessToken { get; set; }
        public string ExpiresIn { get; set; }
        public string RefreshToken { get; set; }
    }
}
