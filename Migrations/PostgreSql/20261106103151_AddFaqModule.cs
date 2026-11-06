using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CasamentoTatianaDiogo.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddFaqModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FaqItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Question = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Answer = table.Column<string>(type: "character varying(6000)", maxLength: 6000, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaqItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FaqPageSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    NavigationLabel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Introduction = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EmptyMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaqPageSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "FaqPageSettings",
                columns: new[] { "Id", "EmptyMessage", "Introduction", "IsEnabled", "NavigationLabel", "Title", "Version" },
                values: new object[] { 1, "Estamos a preparar as respostas às perguntas mais frequentes. Volta em breve!", "Encontra aqui as respostas às dúvidas sobre o nosso dia.", false, "Perguntas frequentes", "Perguntas frequentes", new Guid("00000000-0000-0000-0000-000000000000") });

            migrationBuilder.CreateIndex(
                name: "IX_FaqItems_IsPublished_DisplayOrder",
                table: "FaqItems",
                columns: new[] { "IsPublished", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FaqItems");

            migrationBuilder.DropTable(
                name: "FaqPageSettings");
        }
    }
}
