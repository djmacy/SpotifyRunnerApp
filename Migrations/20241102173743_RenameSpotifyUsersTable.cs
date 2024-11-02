using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpotifyRunnerApp.Migrations
{
    /// <inheritdoc />
    public partial class RenameSpotifyUsersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_SpotifyUsers",
                table: "SpotifyUsers");

            migrationBuilder.RenameTable(
                name: "SpotifyUsers",
                newName: "spotify_user");

            migrationBuilder.AddPrimaryKey(
                name: "PK_spotify_user",
                table: "spotify_user",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_spotify_user",
                table: "spotify_user");

            migrationBuilder.RenameTable(
                name: "spotify_user",
                newName: "SpotifyUsers");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SpotifyUsers",
                table: "SpotifyUsers",
                column: "Id");
        }
    }
}
