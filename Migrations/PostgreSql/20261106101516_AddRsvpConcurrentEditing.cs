using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasamentoTatianaDiogo.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddRsvpConcurrentEditing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RsvpVersion",
                table: "Families",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "RsvpEditSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestId = table.Column<int>(type: "integer", nullable: false),
                    FamilyId = table.Column<int>(type: "integer", nullable: false),
                    ExpectedVersion = table.Column<Guid>(type: "uuid", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RsvpEditSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RsvpEditSessions_RsvpVerificationChallenges_ChallengeId",
                        column: x => x.ChallengeId,
                        principalTable: "RsvpVerificationChallenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RsvpEditSessions_ChallengeId",
                table: "RsvpEditSessions",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_RsvpEditSessions_FamilyId_LastSeenAt",
                table: "RsvpEditSessions",
                columns: new[] { "FamilyId", "LastSeenAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RsvpEditSessions");

            migrationBuilder.DropColumn(
                name: "RsvpVersion",
                table: "Families");
        }
    }
}
