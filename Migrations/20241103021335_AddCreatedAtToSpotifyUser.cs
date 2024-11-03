using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpotifyRunnerApp.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedAtToSpotifyUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ExpiresIn",
                table: "spotify_user",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<long>(
                name: "CreatedAt",
                table: "spotify_user",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "spotify_user");

            migrationBuilder.AlterColumn<string>(
                name: "ExpiresIn",
                table: "spotify_user",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
