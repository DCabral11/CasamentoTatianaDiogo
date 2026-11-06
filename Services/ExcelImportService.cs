using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using CasamentoTatianaDiogo.Common.Extensions;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.ViewModels.Admin;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Services;

public class ExcelImportService(ApplicationDbContext db, IWebHostEnvironment environment, TimeProvider time)
{
    public const int MaxRows = 500;
    public const long MaxBytes = 5 * 1024 * 1024;
    public static readonly string[] FamilyColumns = ["ID", "Nome", "Referência", "Permitir resposta familiar", "Notas"];
    public static readonly string[] GuestColumns = ["ID", "Nome", "Apelido", "Nome apresentado", "E-mail", "Telefone", "Referência da família", "Permitir acompanhante", "Criança", "Notas", "Avatar"];
    public static string[] Columns(string type) => type == "families" ? FamilyColumns : GuestColumns;
    public static string Sheet(string type) => type == "families" ? "Famílias" : "Convidados";
    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public static List<ImportInputRow> ReadRows(IFormFile? file, string type, List<string> errors)
    {
        if (file == null || file.Length == 0 || file.Length > MaxBytes || !Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Seleciona um ficheiro .xlsx até 5 MB.");
            return [];
        }
        try
        {
            using var stream = file.OpenReadStream();
            using var book = new XLWorkbook(stream);
            var sheet = book.Worksheets.FirstOrDefault(x => x.Name.Equals(Sheet(type), StringComparison.OrdinalIgnoreCase));
            if (sheet == null) { errors.Add($"Não foi encontrada a folha «{Sheet(type)}»."); return []; }
            var cells = sheet.Row(1).CellsUsed().ToList();
            if (cells.GroupBy(x => x.GetString().Trim(), StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            { errors.Add("Existem títulos de colunas repetidos. Usa o modelo disponibilizado."); return []; }
            var headers = cells.ToDictionary(x => x.GetString().Trim(), x => x.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);
            // ID is optional so previously downloaded templates remain usable.
            var missing = Columns(type).Where(x => x != "ID" && !headers.ContainsKey(x)).ToList();
            if (missing.Count > 0) { errors.Add($"Faltam as colunas: {string.Join(", ", missing)}."); return []; }
            var last = sheet.LastRowUsed()?.RowNumber() ?? 1;
            if (last > MaxRows + 1) { errors.Add($"O ficheiro pode ter até {MaxRows} linhas de dados."); return []; }
            var result = new List<ImportInputRow>();
            for (var number = 2; number <= last; number++)
            {
                var values = new Dictionary<string, string>();
                foreach (var column in Columns(type))
                {
                    if (!headers.TryGetValue(column, out var index)) { values[column] = ""; continue; }
                    var cell = sheet.Cell(number, index);
                    if (cell.HasFormula) errors.Add($"Linha {number}, {column}: substitui a fórmula pelo valor antes de importar.");
                    values[column] = cell.GetString().Trim();
                }
                if (values.Values.Any(x => x.Length > 0)) result.Add(new() { Number = number, Values = values });
            }
            if (result.Count == 0) errors.Add("O ficheiro não tem linhas preenchidas para importar.");
            return result;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { errors.Add("Não foi possível ler este ficheiro. Usa um Excel .xlsx criado a partir do modelo."); return []; }
    }

    public async Task<List<ImportPlanRow>> BuildPlanAsync(string type, List<ImportInputRow> rows)
    {
        var families = await db.Families.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        var guests = type == "guests" ? await db.Guests.AsNoTracking().OrderBy(x => x.Id).ToListAsync() : [];
        var plan = new List<ImportPlanRow>();
        var targets = new HashSet<int>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var input in rows)
        {
            string Value(string key) => input.Values.GetValueOrDefault(key, "").Trim();
            var row = new ImportPlanRow { Number = input.Number };
            plan.Add(row);
            int? id = null;
            if (Value("ID") != "")
            {
                if (int.TryParse(Value("ID"), out var parsed) && parsed > 0) id = parsed;
                else row.Errors.Add("O ID deve ser um número inteiro positivo ou ficar vazio para novos registos.");
            }
            if (type == "families")
            {
                var reference = Value("Referência");
                var matches = families.Where(x => reference != "" && string.Equals(x.GroupCode, reference, StringComparison.OrdinalIgnoreCase)).ToList();
                Family? existing = id.HasValue ? families.SingleOrDefault(x => x.Id == id) : matches.Count == 1 ? matches[0] : null;
                if (id.HasValue && existing == null) row.Errors.Add("O ID da família não existe.");
                if (matches.Count > 1 || matches.Any(x => x.Id != existing?.Id)) row.Errors.Add("A referência já pertence a outra família ou é ambígua.");
                if (existing == null && families.Any(x => Key(x.Name) == Key(Value("Nome"))))
                    row.Errors.Add("Já existe uma família com este nome. Indica o ID correto para atualizar ou distingue o nome da nova família.");
                var candidate = new Family
                {
                    Name = Value("Nome"), GroupCode = reference.NullIfWhiteSpace(), Notes = Value("Notas").NullIfWhiteSpace(),
                    AllowGroupRsvp = Boolean(Value("Permitir resposta familiar"), true, "Permitir resposta familiar", row.Errors)
                };
                Validate(candidate, row.Errors);
                row.TargetId = existing?.Id;
                row.Before = existing == null ? [] : FamilyValues(existing);
                row.After = FamilyValues(candidate);
                if (!keys.Add(reference != "" ? "ref:" + reference : "name:" + Key(candidate.Name))) row.Errors.Add("Família repetida no ficheiro.");
            }
            else
            {
                var existing = id.HasValue ? guests.SingleOrDefault(x => x.Id == id) : null;
                if (id.HasValue && existing == null) row.Errors.Add("O ID do convidado não existe.");
                var matches = families.Where(x => !string.IsNullOrEmpty(x.GroupCode) && string.Equals(x.GroupCode, Value("Referência da família"), StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count != 1) row.Errors.Add("A referência da família não existe ou é ambígua. Cria ou corrige primeiro a família.");
                row.FamilyId = matches.Count == 1 ? matches[0].Id : 0;
                if (existing != null && existing.FamilyId != row.FamilyId)
                    row.Errors.Add("A mudança de família deve ser feita na ficha do convidado, para rever as ligações ao convite e ao RSVP.");
                var avatar = Value("Avatar");
                if (avatar != "" && (avatar != Path.GetFileName(avatar) || avatar.Contains('/') || avatar.Contains('\\') ||
                    !new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(Path.GetExtension(avatar).ToLowerInvariant()) ||
                    !File.Exists(Path.Combine(environment.WebRootPath, "images", "guests", avatar))))
                    row.Errors.Add("O avatar deve ser o nome de uma imagem existente na pasta de avatares.");
                var candidate = new Guest
                {
                    FirstName = Value("Nome"), LastName = Value("Apelido"),
                    DisplayName = Value("Nome apresentado") == "" ? $"{Value("Nome")} {Value("Apelido")}".Trim() : Value("Nome apresentado"),
                    Email = Value("E-mail").NullIfWhiteSpace(), Phone = Value("Telefone").NullIfWhiteSpace(),
                    FamilyId = row.FamilyId, AvatarFileName = avatar.NullIfWhiteSpace(), Notes = Value("Notas").NullIfWhiteSpace(),
                    AllowPlusOne = Boolean(Value("Permitir acompanhante"), false, "Permitir acompanhante", row.Errors),
                    IsChild = Boolean(Value("Criança"), false, "Criança", row.Errors)
                };
                Validate(candidate, row.Errors);
                candidate.Id = existing?.Id ?? 0;
                candidate.IsPlusOne = existing?.IsPlusOne ?? false;
                candidate.MainGuestId = existing?.MainGuestId;
                foreach (var error in await GuestCompanions.ValidateAsync(db, candidate)) row.Errors.Add(error.Message);
                var sameName = guests.Where(x => x.FamilyId == candidate.FamilyId && Key(x.FirstName) == Key(candidate.FirstName) && Key(x.LastName) == Key(candidate.LastName)).ToList();
                if (existing == null && sameName.Count > 0)
                    row.Errors.Add($"Já existe este nome nesta família (ID: {string.Join(", ", sameName.Select(x => x.Id))}). Indica o ID para atualizar; não é possível escolher apenas pelo nome.");
                row.TargetId = existing?.Id;
                row.Before = existing == null ? [] : GuestValues(existing, families.Single(x => x.Id == existing.FamilyId).GroupCode ?? "");
                row.After = GuestValues(candidate, matches.Count == 1 ? matches[0].GroupCode! : Value("Referência da família"));
                var key = id.HasValue ? "id:" + id : $"new:{candidate.FamilyId}:{Key(candidate.FirstName)}:{Key(candidate.LastName)}";
                if (!keys.Add(key)) row.Errors.Add("Convidado repetido no ficheiro.");
            }
            if (row.TargetId.HasValue && !targets.Add(row.TargetId.Value)) row.Errors.Add("O mesmo registo está indicado em várias linhas.");
            row.Action = row.Errors.Count > 0 ? "Erro" : row.TargetId == null ? "Criar" :
                row.After.All(x => row.Before.GetValueOrDefault(x.Key) == x.Value) ? "Sem alterações" : "Atualizar";
        }
        return plan;
    }

    public async Task<AdminImportDraft> CreateDraftAsync(string type, string owner, List<ImportInputRow> rows)
    {
        await db.AdminImportDrafts.Where(x => x.ExpiresAt < Now).ExecuteDeleteAsync();
        var draft = new AdminImportDraft { OwnerId = owner, ImportType = type, RowsJson = JsonSerializer.Serialize(rows),
            PlanJson = JsonSerializer.Serialize(await BuildPlanAsync(type, rows)), ExpiresAt = Now.AddMinutes(30) };
        db.AdminImportDrafts.Add(draft);
        await db.SaveChangesAsync();
        return draft;
    }

    public Task<AdminImportDraft?> GetDraftAsync(Guid id, string owner) => db.AdminImportDrafts.AsNoTracking()
        .SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == owner && x.ExpiresAt > Now && x.CompletedAt == null);

    public static ImportPreviewViewModel Preview(AdminImportDraft draft) => new()
    {
        Id = draft.Id, Revision = draft.Revision, ImportType = draft.ImportType, ExpiresAt = draft.ExpiresAt,
        Rows = JsonSerializer.Deserialize<List<ImportInputRow>>(draft.RowsJson)!,
        Plan = JsonSerializer.Deserialize<List<ImportPlanRow>>(draft.PlanJson)!, Columns = Columns(draft.ImportType)
    };

    public async Task<bool> ReviewAsync(AdminImportDraft draft, List<ImportInputRow> rows)
    {
        var plan = JsonSerializer.Serialize(await BuildPlanAsync(draft.ImportType, rows));
        var json = JsonSerializer.Serialize(rows);
        return await db.AdminImportDrafts.Where(x => x.Id == draft.Id && x.OwnerId == draft.OwnerId &&
            x.Revision == draft.Revision && x.CompletedAt == null && x.ExpiresAt > Now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RowsJson, json).SetProperty(x => x.PlanJson, plan)
                .SetProperty(x => x.Revision, Guid.NewGuid())) == 1;
    }

    // Serializable validation plus a revision claim prevents stale previews and double submission.
    public async Task<bool> ConfirmAsync(AdminImportDraft draft)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var claimed = await db.AdminImportDrafts.Where(x => x.Id == draft.Id && x.OwnerId == draft.OwnerId &&
                x.Revision == draft.Revision && x.CompletedAt == null && x.ExpiresAt > Now)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.CompletedAt, Now));
            if (claimed != 1) return false;
            var rows = JsonSerializer.Deserialize<List<ImportInputRow>>(draft.RowsJson)!;
            var plan = await BuildPlanAsync(draft.ImportType, rows);
            if (plan.Count == 0 || plan.Any(x => x.Errors.Count > 0) || JsonSerializer.Serialize(plan) != draft.PlanJson)
                return false;
            foreach (var row in plan.Where(x => x.Action != "Sem alterações"))
            {
                var values = row.After;
                if (draft.ImportType == "families")
                {
                    var family = row.TargetId.HasValue ? (await db.Families.FindAsync(row.TargetId.Value))! : new Family();
                    family.Name = values["Nome"]; family.GroupCode = values["Referência"].NullIfWhiteSpace();
                    family.AllowGroupRsvp = values["Permitir resposta familiar"] == "Sim"; family.Notes = values["Notas"].NullIfWhiteSpace();
                    if (!row.TargetId.HasValue) db.Families.Add(family);
                }
                else
                {
                    var guest = row.TargetId.HasValue ? (await db.Guests.FindAsync(row.TargetId.Value))! : new Guest();
                    guest.FirstName = values["Nome"]; guest.LastName = values["Apelido"]; guest.DisplayName = values["Nome apresentado"];
                    guest.Email = values["E-mail"].NullIfWhiteSpace(); guest.Phone = values["Telefone"].NullIfWhiteSpace();
                    guest.FamilyId = row.FamilyId; guest.AllowPlusOne = values["Permitir acompanhante"] == "Sim";
                    guest.IsChild = values["Criança"] == "Sim"; guest.Notes = values["Notas"].NullIfWhiteSpace();
                    guest.AvatarFileName = values["Avatar"].NullIfWhiteSpace();
                    if (!row.TargetId.HasValue) db.Guests.Add(guest);
                    else await GuestCompanions.SyncAsync(db, guest);
                    var family = await db.Families.FindAsync(guest.FamilyId);
                    if (family != null) family.RsvpVersion = Guid.NewGuid();
                }
            }
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        });
    }

    private static string Key(string value) => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).NormalizeForSearch();
    private static void Validate(object candidate, List<string> errors)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(candidate, new ValidationContext(candidate), results, true);
        foreach (var field in results.SelectMany(x => x.MemberNames).Distinct())
            errors.Add(field switch
            {
                "Name" => "Indica o nome da família, com até 150 caracteres.",
                "FirstName" => "Indica o nome do convidado, com até 100 caracteres.",
                "LastName" => "Indica o apelido, com até 100 caracteres.",
                "DisplayName" => "O nome apresentado pode ter até 220 caracteres.",
                "GroupCode" => "A referência pode ter até 50 caracteres.",
                "Email" => "Indica um e-mail válido, com até 200 caracteres, ou deixa o campo vazio.",
                "Phone" => "Indica um telefone válido, com até 50 caracteres, ou deixa o campo vazio.",
                "Notes" => "As notas podem ter até 1000 caracteres.",
                "AvatarFileName" => "O nome do ficheiro de avatar pode ter até 255 caracteres.",
                _ => "Existe um campo inválido. Revê os dados desta linha."
            });
    }
    private static bool Boolean(string value, bool fallback, string column, List<string> errors)
    {
        switch (value.ToLowerInvariant())
        {
            case "": return fallback;
            case "sim": case "s": case "true": case "1": case "x": return true;
            case "não": case "nao": case "n": case "false": case "0": return false;
            default: errors.Add($"«{column}» aceita Sim ou Não."); return fallback;
        }
    }
    private static Dictionary<string, string> FamilyValues(Family f) => new()
    { ["Nome"] = f.Name, ["Referência"] = f.GroupCode ?? "", ["Permitir resposta familiar"] = f.AllowGroupRsvp ? "Sim" : "Não", ["Notas"] = f.Notes ?? "" };
    private static Dictionary<string, string> GuestValues(Guest g, string reference) => new()
    {
        ["Nome"] = g.FirstName, ["Apelido"] = g.LastName, ["Nome apresentado"] = g.DisplayName,
        ["E-mail"] = g.Email ?? "", ["Telefone"] = g.Phone ?? "", ["Referência da família"] = reference,
        ["Permitir acompanhante"] = g.AllowPlusOne ? "Sim" : "Não", ["Criança"] = g.IsChild ? "Sim" : "Não",
        ["Notas"] = g.Notes ?? "", ["Avatar"] = g.AvatarFileName ?? ""
    };
}
