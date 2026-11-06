using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasamentoTatianaDiogo.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class MoveCompanionsIntoGuests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanionGuestId",
                table: "PlusOnes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "PlusOnes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPlusOne",
                table: "Guests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MainGuestId",
                table: "Guests",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlusOnes_CompanionGuestId",
                table: "PlusOnes",
                column: "CompanionGuestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guests_MainGuestId",
                table: "Guests",
                column: "MainGuestId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Guests_Companion",
                table: "Guests",
                sql: "(NOT \"IsPlusOne\" AND \"MainGuestId\" IS NULL) OR (\"IsPlusOne\" AND \"MainGuestId\" IS NOT NULL AND \"MainGuestId\" <> \"Id\" AND NOT \"AllowPlusOne\")");

            migrationBuilder.AddForeignKey(
                name: "FK_Guests_Guests_MainGuestId",
                table: "Guests",
                column: "MainGuestId",
                principalTable: "Guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlusOnes_Guests_CompanionGuestId",
                table: "PlusOnes",
                column: "CompanionGuestId",
                principalTable: "Guests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            // Preserve every legacy companion and its existing RSVP foreign key.
            migrationBuilder.Sql("""
                DO $conversion$
                DECLARE companion record; new_guest_id integer;
                BEGIN
                    FOR companion IN SELECT p.*, g."FamilyId" FROM "PlusOnes" p
                        JOIN "Guests" g ON g."Id" = p."MainGuestId"
                        WHERE p."CompanionGuestId" IS NULL AND NOT p."IsArchived"
                    LOOP
                        UPDATE "Guests" SET "AllowPlusOne" = TRUE WHERE "Id" = companion."MainGuestId";
                        INSERT INTO "Guests" ("FamilyId", "FirstName", "LastName", "DisplayName", "AllowPlusOne",
                            "IsPlusOne", "MainGuestId", "IsChild", "CurrentStatus", "AvatarScale", "AvatarPositionX", "AvatarPositionY")
                        VALUES (companion."FamilyId", companion."PlusOneFirstName", companion."PlusOneLastName",
                            companion."PlusOneDisplayName", FALSE, TRUE, companion."MainGuestId", FALSE,
                            COALESCE((SELECT CASE WHEN r."Status" = 1 AND r."PlusOneAttending" AND r."PlusOneId" = companion."Id"
                                THEN 1 ELSE 2 END FROM "RsvpResponses" r WHERE r."GuestId" = companion."MainGuestId"
                                ORDER BY r."SubmittedAt" DESC, r."Id" DESC LIMIT 1), 0), 1, 50, 50)
                        RETURNING "Id" INTO new_guest_id;
                        UPDATE "PlusOnes" SET "CompanionGuestId" = new_guest_id WHERE "Id" = companion."Id";
                    END LOOP;
                END $conversion$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException("Esta conversão preserva acompanhantes como convidados. Uma reversão exige rever os dados e restaurar uma cópia anterior; não é feita automaticamente.");
    }
}
