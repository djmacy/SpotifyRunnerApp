using Microsoft.EntityFrameworkCore;

namespace SpotifyRunnerApp.Models
{

    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<SpotifyUser> SpotifyUsers { get; set; }
    }
    
}
