using Microsoft.EntityFrameworkCore;
using SpotifyRunnerApp.Models;

namespace SpotifyRunnerApp.Services
{
    public class SpotifyUserService
    {
        private readonly ApplicationDbContext _dbContext;

        public SpotifyUserService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task UpsertUser(string userId, string accessToken, int expiresIn, string refreshToken)
        {
            var existingUser = await _dbContext.spotify_user.FirstOrDefaultAsync(u => u.Username == userId);

            if (existingUser != null)
            {
                existingUser.AccessToken = accessToken;
                existingUser.ExpiresIn = expiresIn.ToString();
                existingUser.RefreshToken = refreshToken;

                _dbContext.spotify_user.Update(existingUser);
            }
            else
            {
                var newUser = new SpotifyUser
                {
                    Username = userId,
                    AccessToken = accessToken,
                    ExpiresIn = expiresIn.ToString(),
                    RefreshToken = refreshToken
                };

                await _dbContext.spotify_user.AddAsync(newUser);
            }

            await _dbContext.SaveChangesAsync();
        }
    }

}
