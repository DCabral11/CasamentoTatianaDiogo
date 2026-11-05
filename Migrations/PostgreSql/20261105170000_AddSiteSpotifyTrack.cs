using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasamentoTatianaDiogo.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddSiteSpotifyTrack : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SpotifyTrackUrl",
                table: "WeddingSettings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpotifyTrackUrl",
                table: "WeddingSettings");
        }
    }
}
