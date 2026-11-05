using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CasamentoTatianaDiogo.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddMusicProgram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MusicSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MusicSections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MusicMoments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Song = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MusicMoments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MusicMoments_MusicSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "MusicSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "MusicSections",
                columns: new[] { "Id", "DisplayOrder", "Name" },
                values: new object[,]
                {
                    { 1, 10, "Cancioneiro Missa" },
                    { 2, 20, "Músicas Quinta" }
                });

            migrationBuilder.InsertData(
                table: "MusicMoments",
                columns: new[] { "Id", "DisplayOrder", "Name", "SectionId", "Song" },
                values: new object[,]
                {
                    { 1, 10, "Entrada Noivo", 1, "Ruínas" },
                    { 2, 20, "Entrada Noiva", 1, "Ninguém te Ama" },
                    { 3, 30, "Ato Penitencial", 1, "Perdoa-me Senhor" },
                    { 4, 40, "Aleluia", 1, "Quero Louvar-te" },
                    { 5, 50, "Salmo", 1, null },
                    { 6, 60, "Alianças", 1, "Deus de Amor (Amor tão Grande)" },
                    { 7, 70, "Ofertório", 1, "Corça" },
                    { 8, 80, "Santo", 1, "Africano" },
                    { 9, 90, "Pai Nosso", 1, "Pai tu és meu Deus" },
                    { 10, 100, "Cordeiro", 1, "Ama Vivendo + Cordeiro" },
                    { 11, 110, "Comunhão", 1, "Pedacinho de Deus" },
                    { 12, 120, "Ação de Graças", 1, "Cristo em Mim" },
                    { 13, 130, "Consagração a Nossa Senhora", 1, "Consagração a Nossa Senhora" },
                    { 14, 140, "Final", 1, "Sabes Bem" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_MusicMoments_SectionId",
                table: "MusicMoments",
                column: "SectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MusicMoments");

            migrationBuilder.DropTable(
                name: "MusicSections");
        }
    }
}
