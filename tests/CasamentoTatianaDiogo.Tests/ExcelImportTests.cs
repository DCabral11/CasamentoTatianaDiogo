using System.Net;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Models.Enums;
using CasamentoTatianaDiogo.Services;
using CasamentoTatianaDiogo.ViewModels.Admin;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasamentoTatianaDiogo.Tests;

public class ExcelImportTests
{
    [Fact]
    public async Task FamiliesArePreviewedEditedAndConfirmedOnceIncludingExistingReferences()
    {
        using var app = new HttpTests.TestApplication();
        using var client = app.Client();
        await Login(app, client);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Families.Add(new() { Name = "Nome anterior", GroupCode = "F01", Notes = "Nota anterior" });
        await db.SaveChangesAsync();
        var rows = new[] { FamilyRow("Nome novo", "F01"), FamilyRow("Família nova", "F02") };
        var upload = await Upload(client, "families", rows, includeId: false);
        Assert.Equal(HttpStatusCode.Redirect, upload.StatusCode);
        var preview = WebUtility.HtmlDecode(await client.GetStringAsync(upload.Headers.Location));
        Assert.Contains("Nome anterior", preview);
        Assert.Contains("Nome novo", preview);
        Assert.Contains("Será alterado", preview);
        Assert.Contains("Vazio", preview);
        Assert.Single(await db.Families.AsNoTracking().ToListAsync());
        var draft = await db.AdminImportDrafts.AsNoTracking().SingleAsync();
        var edit = await client.GetStringAsync($"/Admin/ExcelImport/Edit?id={draft.Id}");
        Assert.Contains("Rows[0].Values[Nome]", edit);
        var input = ExcelImportService.Preview(draft).Rows;
        input[0].Values["Nome"] = "Nome revisto";
        var review = await Review(client, draft, input);
        Assert.Equal(HttpStatusCode.Redirect, review.StatusCode);
        Assert.Equal("Nome anterior", await db.Families.AsNoTracking().Select(x => x.Name).SingleAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await Post(client, "Confirm", draft)).StatusCode);
        Assert.Single(await db.Families.AsNoTracking().ToListAsync()); // old revision cannot apply
        draft = await db.AdminImportDrafts.AsNoTracking().SingleAsync();
        var confirm = await Post(client, "Confirm", draft);
        Assert.Equal("/Admin/InvitationGroups/Index", confirm.Headers.Location!.OriginalString);
        Assert.Equal(2, await db.Families.CountAsync());
        var updated = await db.Families.AsNoTracking().SingleAsync(x => x.GroupCode == "F01");
        Assert.Equal("Nome revisto", updated.Name);
        Assert.Null(updated.Notes);
        Assert.Equal(HttpStatusCode.Gone, (await Post(client, "Confirm", draft)).StatusCode);
        Assert.Equal(2, await db.Families.CountAsync());
    }

    [Fact]
    public async Task GuestsRequireExplicitIdsAndErrorsBlockEntireBatchUntilCorrected()
    {
        using var app = new HttpTests.TestApplication();
        using var client = app.Client();
        await Login(app, client);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var guest = new Guest { FirstName = "Ana", LastName = "Silva", DisplayName = "Ana Silva", CurrentStatus = RsvpStatus.Attending,
            Family = new() { Name = "Silva", GroupCode = "S01" }, AvatarScale = 1.4 };
        db.Guests.Add(guest);
        await db.SaveChangesAsync();
        db.RsvpResponses.Add(new() { GuestId = guest.Id, Status = RsvpStatus.Attending, Message = "Resposta privada" });
        db.PlusOnes.Add(new() { MainGuestId = guest.Id, PlusOneDisplayName = "Acompanhante" });
        await db.SaveChangesAsync();
        var rows = new[] { GuestRow("Ana", "Silva", "S01"), GuestRow("Bruno", "Silva", "MISSING") };
        var upload = await Upload(client, "guests", rows);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(upload.Headers.Location));
        Assert.Contains("Indica o ID para atualizar", html);
        Assert.Contains("referência da família não existe", html);
        var draft = await db.AdminImportDrafts.AsNoTracking().SingleAsync();
        await Post(client, "Confirm", draft);
        Assert.Single(await db.Guests.ToListAsync());
        var input = ExcelImportService.Preview(draft).Rows;
        input[0].Values["ID"] = guest.Id.ToString();
        input[0].Values["E-mail"] = "new@example.test";
        input[0].Values["Permitir acompanhante"] = "Sim";
        input[1].Values["Referência da família"] = "S01";
        input[1].Values["Notas"] = "<script>alert('test')</script>";
        await Review(client, draft, input);
        draft = await db.AdminImportDrafts.AsNoTracking().SingleAsync();
        html = await client.GetStringAsync($"/Admin/ExcelImport/Preview?id={draft.Id}");
        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("&lt;script&gt;", html);
        var confirm = await Post(client, "Confirm", draft);
        Assert.Equal("/Admin/Guests/Index", confirm.Headers.Location!.OriginalString);
        Assert.Equal(2, await db.Guests.CountAsync());
        var saved = await db.Guests.AsNoTracking().SingleAsync(x => x.Id == guest.Id);
        Assert.Equal("new@example.test", saved.Email);
        Assert.Equal(RsvpStatus.Attending, saved.CurrentStatus);
        Assert.Equal(1.4, saved.AvatarScale);
        Assert.Equal("Resposta privada", await db.RsvpResponses.Select(x => x.Message).SingleAsync());
        Assert.Single(await db.PlusOnes.ToListAsync());
    }

    [Fact]
    public async Task DatabaseChangesRequireASecondReviewAndDoNotPartiallyApply()
    {
        using var app = new HttpTests.TestApplication();
        using var client = app.Client();
        await Login(app, client);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var family = new Family { Name = "Antes", GroupCode = "F01" };
        db.Families.Add(family);
        await db.SaveChangesAsync();
        await Upload(client, "families", [FamilyRow("Depois", "F01"), FamilyRow("Nova", "F02")]);
        var draft = await db.AdminImportDrafts.AsNoTracking().SingleAsync();
        family.Name = "Alterado noutro separador";
        await db.SaveChangesAsync();
        await Post(client, "Confirm", draft);
        Assert.Single(await db.Families.ToListAsync());
        Assert.Equal("Alterado noutro separador", await db.Families.AsNoTracking().Select(x => x.Name).SingleAsync());
        var refreshed = await db.AdminImportDrafts.AsNoTracking().SingleAsync();
        Assert.NotEqual(draft.Revision, refreshed.Revision);
        Assert.Contains("Alterado noutro separador", WebUtility.HtmlDecode(await client.GetStringAsync($"/Admin/ExcelImport/Preview?id={draft.Id}")));
        await Post(client, "Confirm", refreshed);
        Assert.Equal(2, await db.Families.CountAsync());
    }

    [Fact]
    public async Task DraftsArePrivateExpiringCancellableAndAllMutationsRequireCsrf()
    {
        var time = new MutableTime();
        using var app = new HttpTests.TestApplication(time: time);
        using var client = app.Client();
        using var anonymous = app.Client();
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync("/Admin/ExcelImport/Families")).StatusCode);
        await Login(app, client);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.GetAsync("/Admin/ExcelImport/ImportFamilies")).StatusCode);
        foreach (var action in new[] { "ImportFamilies", "ImportGuests", "Confirm", "Review", "Cancel" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Admin/ExcelImport/" + action, new FormUrlEncodedContent([]))).StatusCode);
        await Upload(client, "families", [FamilyRow("Nova", "F01")]);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var draft = await db.AdminImportDrafts.AsNoTracking().SingleAsync();
        using var secondAdmin = app.Client();
        await Login(app, secondAdmin, "second@example.test");
        Assert.Equal(HttpStatusCode.Gone, (await secondAdmin.GetAsync($"/Admin/ExcelImport/Preview?id={draft.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await Post(secondAdmin, "Confirm", draft)).StatusCode);
        Assert.Contains("no-store", (await client.GetAsync($"/Admin/ExcelImport/Preview?id={draft.Id}")).Headers.CacheControl!.ToString());
        time.Advance(TimeSpan.FromMinutes(31));
        Assert.Equal(HttpStatusCode.Gone, (await Post(client, "Confirm", draft)).StatusCode);
        Assert.Empty(await db.Families.ToListAsync());
        await Upload(client, "families", [FamilyRow("Nova", "F01")]);
        draft = await db.AdminImportDrafts.AsNoTracking().SingleAsync();
        await Post(client, "Cancel", draft);
        Assert.Empty(await db.AdminImportDrafts.ToListAsync());
        Assert.Empty(await db.Families.ToListAsync());
    }

    [Fact]
    public async Task DuplicateRowsAndInvalidBooleansCannotBeConfirmedAndTemplatesIncludeIds()
    {
        using var app = new HttpTests.TestApplication();
        using var client = app.Client();
        await Login(app, client);
        using var template = new XLWorkbook(new MemoryStream(await client.GetByteArrayAsync("/Admin/ExcelImport/DownloadGuestsTemplate")));
        Assert.Equal("ID", template.Worksheet("Convidados").Cell(1, 1).GetString());
        var first = FamilyRow("Nova", "F01");
        first["Permitir resposta familiar"] = "talvez";
        var upload = await Upload(client, "families", [first, FamilyRow("Nova", "f01")]);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(upload.Headers.Location));
        Assert.Contains("aceita Sim ou Não", html);
        Assert.Contains("repetida no ficheiro", html);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await Post(client, "Confirm", await db.AdminImportDrafts.AsNoTracking().SingleAsync());
        Assert.Empty(await db.Families.ToListAsync());
    }

    private static Dictionary<string, string> FamilyRow(string name, string reference) => new() { ["Nome"] = name, ["Referência"] = reference };
    private static Dictionary<string, string> GuestRow(string name, string last, string reference) => new()
        { ["Nome"] = name, ["Apelido"] = last, ["Referência da família"] = reference };

    private static async Task Login(HttpTests.TestApplication app, HttpClient client, string email = "import@example.test")
    {
        using var scope = app.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync("Admin")) await roles.CreateAsync(new IdentityRole("Admin"));
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        Assert.True((await users.CreateAsync(admin, "ImportTest!2027")).Succeeded);
        await users.AddToRoleAsync(admin, "Admin");
        var csrf = await HttpTests.AntiforgeryToken(client, "/Admin/Login");
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Admin/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["email"] = email, ["password"] = "ImportTest!2027", ["__RequestVerificationToken"] = csrf }))).StatusCode);
    }

    private static async Task<HttpResponseMessage> Upload(HttpClient client, string type, Dictionary<string, string>[] rows, bool includeId = true)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(ExcelImportService.Sheet(type));
        var columns = ExcelImportService.Columns(type).Where(x => includeId || x != "ID").ToArray();
        for (var c = 0; c < columns.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = columns[c];
            for (var r = 0; r < rows.Length; r++) sheet.Cell(r + 2, c + 1).Value = rows[r].GetValueOrDefault(columns[c], "");
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(stream.ToArray()), "File", "import.xlsx");
        form.Add(new StringContent(await HttpTests.AntiforgeryToken(client, "/Admin/ExcelImport/" + (type == "families" ? "Families" : "Guests"))), "__RequestVerificationToken");
        return await client.PostAsync("/Admin/ExcelImport/" + (type == "families" ? "ImportFamilies" : "ImportGuests"), form);
    }
    private static async Task<HttpResponseMessage> Post(HttpClient client, string action, AdminImportDraft draft, Dictionary<string, string>? fields = null)
    {
        fields ??= [];
        fields["id"] = draft.Id.ToString(); fields["revision"] = draft.Revision.ToString();
        fields["__RequestVerificationToken"] = await HttpTests.AntiforgeryToken(client, "/Admin/ExcelImport/Families");
        return await client.PostAsync("/Admin/ExcelImport/" + action, new FormUrlEncodedContent(fields));
    }
    private static Task<HttpResponseMessage> Review(HttpClient client, AdminImportDraft draft, List<ImportInputRow> rows)
    {
        var fields = new Dictionary<string, string>();
        for (var i = 0; i < rows.Count; i++)
        {
            fields[$"Rows[{i}].Number"] = rows[i].Number.ToString();
            foreach (var value in rows[i].Values) fields[$"Rows[{i}].Values[{value.Key}]"] = value.Value;
        }
        return Post(client, "Review", draft, fields);
    }
}
