using Microsoft.EntityFrameworkCore;
using SpotifyRunnerApp.Models;

namespace SpotifyRunnerApp.Services
{
    public class SpotifyUserService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly SpotifyAPIService _spotifyAPIService;

        public SpotifyUserService(ApplicationDbContext dbContext, SpotifyAPIService spotifyAPIService)
        {
            _dbContext = dbContext;
            _spotifyAPIService = spotifyAPIService;
        }

        public async Task UpsertUser(string userId, string accessToken, int expiresIn, string refreshToken)
        {
            var existingUser = await _dbContext.spotify_user.FirstOrDefaultAsync(u => u.Username == userId);

            if (existingUser != null)
            {
                existingUser.AccessToken = accessToken;
                existingUser.ExpiresIn = expiresIn;
                existingUser.RefreshToken = refreshToken;
                existingUser.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); // Update the timestamp

                _dbContext.spotify_user.Update(existingUser);
            }
            else
            {
                var newUser = new SpotifyUser
                {
                    Username = userId,
                    AccessToken = accessToken,
                    ExpiresIn = expiresIn,
                    RefreshToken = refreshToken,
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                await _dbContext.spotify_user.AddAsync(newUser);
            }

            await _dbContext.SaveChangesAsync();
        }

        // Method to get the AccessToken for a given user by their Username
        public async Task<string> GetAccessTokenByUsername(string username)
        {
            long current = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            // Awaiting the asynchronous database call
            var user = await _dbContext.Set<SpotifyUser>().FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                throw new Exception("User not found");
            }
            long created = user.CreatedAt;
            long expired = user.ExpiresIn + created;
            if (expired <= current)
            {
                var newToken = await _spotifyAPIService.RefreshAccessToken(user.RefreshToken);
                await UpsertUser(user.Username, newToken.AccessToken, newToken.ExpiresIn, newToken.RefreshToken);
                return newToken.AccessToken;
                //If expired perform logic to grab new token using the refresh token from user record and upsert new info into database
            }
            
            // Check if user is null before accessing AccessToken
            return user.AccessToken; // Return the access token or null if not found
        }
    }

}
