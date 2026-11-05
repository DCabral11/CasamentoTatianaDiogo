using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasamentoTatianaDiogo.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddRsvpContactVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RsvpInvitationContacts",
                columns: table => new
                {
                    FamilyId = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    NormalizedContact = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RsvpInvitationContacts", x => x.FamilyId);
                    table.ForeignKey(
                        name: "FK_RsvpInvitationContacts_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RsvpVerificationChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestId = table.Column<int>(type: "integer", nullable: false),
                    FamilyId = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    NormalizedContact = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SessionExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RsvpVerificationChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RsvpVerificationChallenges_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RsvpVerificationLimits",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WindowEndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextAllowedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RsvpVerificationLimits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RsvpInvitationContacts_Channel_NormalizedContact",
                table: "RsvpInvitationContacts",
                columns: new[] { "Channel", "NormalizedContact" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RsvpVerificationChallenges_CreatedAt",
                table: "RsvpVerificationChallenges",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RsvpVerificationChallenges_FamilyId_Channel_NormalizedConta~",
                table: "RsvpVerificationChallenges",
                columns: new[] { "FamilyId", "Channel", "NormalizedContact" });

            migrationBuilder.CreateIndex(
                name: "IX_RsvpVerificationChallenges_GuestId",
                table: "RsvpVerificationChallenges",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_RsvpVerificationLimits_WindowEndsAt",
                table: "RsvpVerificationLimits",
                column: "WindowEndsAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RsvpInvitationContacts");

            migrationBuilder.DropTable(
                name: "RsvpVerificationChallenges");

            migrationBuilder.DropTable(
                name: "RsvpVerificationLimits");
        }
    }
}
