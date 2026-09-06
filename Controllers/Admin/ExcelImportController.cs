using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Common.Extensions;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.ViewModels.Admin;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace CasamentoTatianaDiogo.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]")]
    public class ExcelImportController(ApplicationDbContext db, IWebHostEnvironment environment) : Controller
    {
        private static readonly string[] FamilyColumns = ["Nome", "Referência", "Permitir resposta familiar", "Notas"];
        private static readonly string[] GuestColumns = ["Nome", "Apelido", "Nome apresentado", "E-mail", "Telefone", "Referência da família", "Permitir acompanhante", "Criança", "Notas", "Avatar"];

        public IActionResult Families() => View("~/Views/Admin/ExcelImport/Index.cshtml", CreateViewModel("families"));

        public IActionResult Guests() => View("~/Views/Admin/ExcelImport/Index.cshtml", CreateViewModel("guests"));

        [HttpGet]
        public FileResult DownloadFamiliesTemplate() => CreateTemplate("Famílias", FamilyColumns, "Preenche uma linha por família. A referência é opcional, mas deve ser única quando existir.", "familias-template.xlsx");

        [HttpGet]
        public FileResult DownloadGuestsTemplate() => CreateTemplate("Convidados", GuestColumns, "Preenche uma linha por convidado. A referência da família tem de corresponder a uma referência já criada nas Famílias.", "convidados-template.xlsx");

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportFamilies(ExcelImportViewModel model)
        {
            var errors = new List<string>();
            var rows = ReadRows(model.File, "Famílias", FamilyColumns, errors);
            var newFamilies = new List<Family>();
            var existingReferences = await db.Families
                .Where(f => f.GroupCode != null)
                .Select(f => f.GroupCode!)
                .ToListAsync();
            var references = new HashSet<string>(existingReferences, StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var name = Value(row, "Nome");
                var reference = Value(row, "Referência");
                var notes = Value(row, "Notas");

                if (string.IsNullOrWhiteSpace(name))
                    errors.Add($"Linha {row.Number}: indica o nome da família.");
                else if (name.Length > 150)
                    errors.Add($"Linha {row.Number}: o nome da família pode ter no máximo 150 caracteres.");

                if (reference.Length > 50)
                    errors.Add($"Linha {row.Number}: a referência pode ter no máximo 50 caracteres.");
                else if (!string.IsNullOrWhiteSpace(reference) && !references.Add(reference))
                    errors.Add($"Linha {row.Number}: a referência «{reference}» já existe ou está repetida no ficheiro.");

                if (notes.Length > 1000)
                    errors.Add($"Linha {row.Number}: as notas podem ter no máximo 1000 caracteres.");

                var allowGroupRsvp = ReadBoolean(Value(row, "Permitir resposta familiar"), true, row.Number, "Permitir resposta familiar", errors);
                newFamilies.Add(new Family { Name = name, GroupCode = EmptyToNull(reference), Notes = EmptyToNull(notes), AllowGroupRsvp = allowGroupRsvp });
            }

            if (errors.Count > 0)
                return ImportError("families", errors);

            if (newFamilies.Count == 0)
                return ImportError("families", ["O ficheiro não tem linhas preenchidas para importar."]);

            db.Families.AddRange(newFamilies);
            await db.SaveChangesAsync();
            TempData["Success"] = $"Foram importadas {newFamilies.Count} famílias.";
            return RedirectToAction("Index", "InvitationGroups");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportGuests(ExcelImportViewModel model)
        {
            var errors = new List<string>();
            var rows = ReadRows(model.File, "Convidados", GuestColumns, errors);
            var familyByReference = await db.Families
                .Where(f => f.GroupCode != null)
                .ToDictionaryAsync(f => f.GroupCode!, f => f.Id, StringComparer.OrdinalIgnoreCase);
            var avatarsDirectory = Path.Combine(environment.WebRootPath, "images", "guests");
            var guests = new List<Guest>();
            var emailValidator = new EmailAddressAttribute();

            foreach (var row in rows)
            {
                var firstName = Value(row, "Nome");
                var lastName = Value(row, "Apelido");
                var displayName = Value(row, "Nome apresentado");
                var email = Value(row, "E-mail");
                var phone = Value(row, "Telefone");
                var familyReference = Value(row, "Referência da família");
                var notes = Value(row, "Notas");
                var avatar = Path.GetFileName(Value(row, "Avatar"));

                if (string.IsNullOrWhiteSpace(firstName) || firstName.Length > 100)
                    errors.Add($"Linha {row.Number}: indica um nome com no máximo 100 caracteres.");
                if (string.IsNullOrWhiteSpace(lastName) || lastName.Length > 100)
                    errors.Add($"Linha {row.Number}: indica um apelido com no máximo 100 caracteres.");
                if (displayName.Length > 220)
                    errors.Add($"Linha {row.Number}: o nome apresentado pode ter no máximo 220 caracteres.");
                if (!string.IsNullOrWhiteSpace(email) && (email.Length > 200 || !emailValidator.IsValid(email)))
                    errors.Add($"Linha {row.Number}: indica um e-mail válido.");
                if (phone.Length > 50)
                    errors.Add($"Linha {row.Number}: o telefone pode ter no máximo 50 caracteres.");
                if (notes.Length > 1000)
                    errors.Add($"Linha {row.Number}: as notas podem ter no máximo 1000 caracteres.");
                var familyId = 0;
                if (string.IsNullOrWhiteSpace(familyReference) || !familyByReference.TryGetValue(familyReference, out familyId))
                    errors.Add($"Linha {row.Number}: a referência da família «{familyReference}» não existe. Importa ou cria primeiro a família.");
                if (!string.IsNullOrWhiteSpace(avatar) && !System.IO.File.Exists(Path.Combine(avatarsDirectory, avatar)))
                    errors.Add($"Linha {row.Number}: o avatar «{avatar}» não existe na pasta de avatares.");

                var allowPlusOne = ReadBoolean(Value(row, "Permitir acompanhante"), false, row.Number, "Permitir acompanhante", errors);
                var isChild = ReadBoolean(Value(row, "Criança"), false, row.Number, "Criança", errors);
                guests.Add(new Guest
                {
                    FirstName = firstName,
                    LastName = lastName,
                    DisplayName = string.IsNullOrWhiteSpace(displayName) ? $"{firstName} {lastName}".Trim() : displayName,
                    Email = EmptyToNull(email),
                    Phone = EmptyToNull(phone),
                    FamilyId = familyId,
                    AllowPlusOne = allowPlusOne,
                    IsChild = isChild,
                    Notes = EmptyToNull(notes),
                    AvatarFileName = EmptyToNull(avatar)
                });
            }

            if (errors.Count > 0)
                return ImportError("guests", errors);

            if (guests.Count == 0)
                return ImportError("guests", ["O ficheiro não tem linhas preenchidas para importar."]);

            db.Guests.AddRange(guests);
            await db.SaveChangesAsync();
            TempData["Success"] = $"Foram importados {guests.Count} convidados.";
            return RedirectToAction("Index", "Guests");
        }

        private IActionResult ImportError(string importType, IReadOnlyList<string> errors)
        {
            ViewBag.ImportErrors = errors;
            return View("~/Views/Admin/ExcelImport/Index.cshtml", CreateViewModel(importType));
        }

        private static ExcelImportViewModel CreateViewModel(string importType) => importType == "families"
            ? new ExcelImportViewModel
            {
                ImportType = "families", Title = "Importar famílias", Description = "Carrega o modelo preenchido para criar várias famílias de uma só vez.", TemplateAction = nameof(DownloadFamiliesTemplate), SubmitAction = nameof(ImportFamilies), ExpectedSheetName = "Famílias", Columns = FamilyColumns
            }
            : new ExcelImportViewModel
            {
                ImportType = "guests", Title = "Importar convidados", Description = "Usa a referência única da família para associar cada convidado corretamente.", TemplateAction = nameof(DownloadGuestsTemplate), SubmitAction = nameof(ImportGuests), ExpectedSheetName = "Convidados", Columns = GuestColumns
            };

        private static List<ImportRow> ReadRows(IFormFile? file, string sheetName, IReadOnlyList<string> columns, List<string> errors)
        {
            if (file is null || file.Length == 0)
            {
                errors.Add("Seleciona um ficheiro Excel preenchido antes de continuar.");
                return [];
            }

            if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Seleciona um ficheiro no formato .xlsx.");
                return [];
            }

            try
            {
                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var sheet = workbook.Worksheets.FirstOrDefault(w => string.Equals(w.Name, sheetName, StringComparison.OrdinalIgnoreCase));
                if (sheet is null)
                {
                    errors.Add($"Não foi encontrada a folha «{sheetName}». Usa o modelo disponibilizado.");
                    return [];
                }

                var headers = sheet.Row(1).CellsUsed().ToDictionary(cell => cell.GetString().Trim(), cell => cell.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);
                var missingColumns = columns.Where(column => !headers.ContainsKey(column)).ToList();
                if (missingColumns.Count > 0)
                {
                    errors.Add($"Faltam as colunas: {string.Join(", ", missingColumns)}.");
                    return [];
                }

                var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
                var rows = new List<ImportRow>();
                for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
                {
                    var values = headers.ToDictionary(pair => pair.Key, pair => sheet.Cell(rowNumber, pair.Value).GetString().Trim(), StringComparer.OrdinalIgnoreCase);
                    if (values.Values.All(string.IsNullOrWhiteSpace))
                        continue;
                    rows.Add(new ImportRow(rowNumber, values));
                }

                return rows;
            }
            catch (Exception)
            {
                errors.Add("Não foi possível ler este ficheiro. Confirma que é um Excel .xlsx criado a partir do modelo.");
                return [];
            }
        }

        private static bool ReadBoolean(string value, bool defaultValue, int rowNumber, string columnName, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            return value.Trim().ToLowerInvariant() switch
            {
                "sim" or "s" or "true" or "1" or "x" => true,
                "não" or "nao" or "n" or "false" or "0" => false,
                _ => InvalidBoolean(defaultValue, rowNumber, columnName, errors)
            };
        }

        private static bool InvalidBoolean(bool defaultValue, int rowNumber, string columnName, List<string> errors)
        {
            errors.Add($"Linha {rowNumber}: «{columnName}» aceita Sim ou Não.");
            return defaultValue;
        }

        private static string Value(ImportRow row, string column) => row.Values.GetValueOrDefault(column, string.Empty).Trim();
        private static string? EmptyToNull(string value) => value.NullIfWhiteSpace();

        private static FileResult CreateTemplate(string sheetName, IReadOnlyList<string> headers, string note, string fileName)
        {
            using var workbook = new XLWorkbook();
            var instructions = workbook.Worksheets.Add("Instruções");
            instructions.Cell("A1").Value = "Importação de dados";
            instructions.Cell("A2").Value = note;
            instructions.Cell("A4").Value = "Não alteres os títulos das colunas nem o nome da folha de dados.";
            instructions.Column(1).Width = 110;
            instructions.Range("A1:A1").Style.Font.Bold = true;
            instructions.Range("A1:A1").Style.Font.FontSize = 16;
            instructions.Range("A2:A4").Style.Alignment.WrapText = true;

            var data = workbook.Worksheets.Add(sheetName);
            for (var index = 0; index < headers.Count; index++)
                data.Cell(1, index + 1).Value = headers[index];

            var headerRange = data.Range(1, 1, 1, headers.Count);
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#BD4742");
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            data.SheetView.FreezeRows(1);
            data.Columns(1, headers.Count).AdjustToContents();
            for (var index = 1; index <= headers.Count; index++)
                data.Column(index).Width = Math.Max(data.Column(index).Width, 18);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return new FileContentResult(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet") { FileDownloadName = fileName };
        }

        private sealed record ImportRow(int Number, Dictionary<string, string> Values);
    }
}
