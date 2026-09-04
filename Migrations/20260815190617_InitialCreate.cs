using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasamentoTatianaDiogo.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Families",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    GroupCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AllowGroupRsvp = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Families", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PhotoUploads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    GoogleDriveFileId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GoogleDriveWebViewLink = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GuestName = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UploadIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoUploads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PhotoUploadSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    OpensAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosesAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GoogleDriveFolderId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MaxFilesSizeMb = table.Column<int>(type: "int", nullable: false),
                    AllowedExtensionsCsv = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoUploadSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimelineEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EventDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IconName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimelineEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeddingSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WeddingDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CeremonyLocationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CeremonyAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CeremonyGoogleMapsUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CeremonyGoogleMapsEmbedUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReceptionLocationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReceptionAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReceptionGoogleMapsUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReceptionGoogleMapsEmbedUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    BrideName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    BridePhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BrideEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GroomName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    GroomPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    GroomEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CoupleStory = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HomeHeroTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HomeHeroSubtitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    LogoPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PrimaryColor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SecondaryColor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeddingSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Guests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FamilyId = table.Column<int>(type: "int", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AllowPlusOne = table.Column<bool>(type: "bit", nullable: false),
                    IsChild = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CurrentStatus = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Guests_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlusOnes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MainGuestId = table.Column<int>(type: "int", nullable: false),
                    PlusOneFirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PlusOneLastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PlusOneDisplayName = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                    PlusOneDietaryRestrictions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlusOnes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlusOnes_Guests_MainGuestId",
                        column: x => x.MainGuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RsvpResponses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GuestId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DietaryRestrictions = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MusicRequest = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PlusOneAttending = table.Column<bool>(type: "bit", nullable: false),
                    PlusOneId = table.Column<int>(type: "int", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedFromIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RsvpResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RsvpResponses_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RsvpResponses_PlusOnes_PlusOneId",
                        column: x => x.PlusOneId,
                        principalTable: "PlusOnes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Families_GroupCode",
                table: "Families",
                column: "GroupCode",
                unique: true,
                filter: "[GroupCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Families_Name",
                table: "Families",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_FamilyId",
                table: "Guests",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_FirstName_LastName",
                table: "Guests",
                columns: new[] { "FirstName", "LastName" });

            migrationBuilder.CreateIndex(
                name: "IX_PhotoUploads_UploadedAt",
                table: "PhotoUploads",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PlusOnes_MainGuestId",
                table: "PlusOnes",
                column: "MainGuestId");

            migrationBuilder.CreateIndex(
                name: "IX_RsvpResponses_GuestId_SubmittedAt",
                table: "RsvpResponses",
                columns: new[] { "GuestId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RsvpResponses_PlusOneId",
                table: "RsvpResponses",
                column: "PlusOneId");

            migrationBuilder.CreateIndex(
                name: "IX_TimelineEvents_IsActive_DisplayOrder_EventDateTime",
                table: "TimelineEvents",
                columns: new[] { "IsActive", "DisplayOrder", "EventDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_WeddingSettings_Id",
                table: "WeddingSettings",
                column: "Id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "PhotoUploads");

            migrationBuilder.DropTable(
                name: "PhotoUploadSettings");

            migrationBuilder.DropTable(
                name: "RsvpResponses");

            migrationBuilder.DropTable(
                name: "TimelineEvents");

            migrationBuilder.DropTable(
                name: "WeddingSettings");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "PlusOnes");

            migrationBuilder.DropTable(
                name: "Guests");

            migrationBuilder.DropTable(
                name: "Families");
        }
    }
}
