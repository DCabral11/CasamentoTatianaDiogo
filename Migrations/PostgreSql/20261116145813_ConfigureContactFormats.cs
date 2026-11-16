using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasamentoTatianaDiogo.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class ConfigureContactFormats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultEmailDomain",
                table: "WeddingSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailDomainDisplay",
                table: "WeddingSettings",
                type: "text",
                nullable: false,
                defaultValue: "unicode");

            migrationBuilder.AddColumn<string>(
                name: "ExtensionLabel",
                table: "WeddingSettings",
                type: "text",
                nullable: false,
                defaultValue: "ext.");

            migrationBuilder.AddColumn<string>(
                name: "PhoneDisplay",
                table: "WeddingSettings",
                type: "text",
                nullable: false,
                defaultValue: "international");

            migrationBuilder.AddColumn<string>(
                name: "PhoneRegion",
                table: "WeddingSettings",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "PT");

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Suppliers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultEmailDomain",
                table: "WeddingSettings");

            migrationBuilder.DropColumn(
                name: "EmailDomainDisplay",
                table: "WeddingSettings");

            migrationBuilder.DropColumn(
                name: "ExtensionLabel",
                table: "WeddingSettings");

            migrationBuilder.DropColumn(
                name: "PhoneDisplay",
                table: "WeddingSettings");

            migrationBuilder.DropColumn(
                name: "PhoneRegion",
                table: "WeddingSettings");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Suppliers");
        }
    }
}
