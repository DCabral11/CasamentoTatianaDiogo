#region References

using System.Data.Common;
using System.Security.Claims;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Services;
using CasamentoTatianaDiogo.ViewModels.Admin;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

#endregion

namespace CasamentoTatianaDiogo.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class ExcelImportController(ApplicationDbContext db, ExcelImportService imports) : Controller
    {
        #region Properties

        private string Owner => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        private const string ViewPath = "~/Views/Admin/ExcelImport/";

        #endregion

        #region Controller - GET Methods

        [HttpGet]
        public async Task<IActionResult> Preview(Guid id)
        {
            var draft = await imports.GetDraftAsync(id, Owner);

            return draft == null ? Unavailable() : View(ViewPath + "Preview.cshtml", ExcelImportService.Preview(draft));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            var draft = await imports.GetDraftAsync(id, Owner);

            if (draft == null)
                return Unavailable();

            var preview = ExcelImportService.Preview(draft);

            return preview.CanConfirm ? RedirectToAction(nameof(Preview), new { id }) : View(ViewPath + "Edit.cshtml", preview);
        }

        [HttpGet]
        public IActionResult Families() => View(ViewPath + "Index.cshtml", CreateViewModel("families"));

        [HttpGet]
        public IActionResult Guests() => View(ViewPath + "Index.cshtml", CreateViewModel("guests"));

        [HttpGet] 
        public FileResult DownloadFamiliesTemplate() => Template("families");

        [HttpGet] 
        public FileResult DownloadGuestsTemplate() => Template("guests");

        #endregion

        #region Controller - POST Methods

        [HttpPost, ValidateAntiForgeryToken, RequestFormLimits(ValueCountLimit = 15000)]
        public async Task<IActionResult> Review(Guid id, Guid revision, List<ImportInputRow> rows)
        {
            var draft = await imports.GetDraftAsync(id, Owner);

            if (draft == null)
                return Unavailable();

            if (draft.Revision != revision)
                return Changed(id);

            var preview = ExcelImportService.Preview(draft);
            var errorNumbers = preview.Plan.Where(x => x.Errors.Count > 0).Select(x => x.Number);

            if (!ModelState.IsValid || rows.Count == 0 || !rows.Select(x => x.Number).SequenceEqual(errorNumbers) || rows.Any(x => x.Values == null || x.Values.Count != preview.Columns.Length || preview.Columns.Any(c => !x.Values.ContainsKey(c) || (ExcelImportService.IsBooleanColumn(c) && x.Values[c] is not ("Sim" or "Não"))) || x.Values.Values.Any(v => v?.Length > 2000)))
                return BadRequest(T("A correção é inválida. Volta à pré-visualização e revê as linhas com erros."));

            var originals = preview.Rows.ToDictionary(x => x.Number);

            foreach (var row in rows)
                originals[row.Number].Values = preview.Columns.ToDictionary(c => c, c => row.Values[c] ?? "");

            if (!await imports.ReviewAsync(draft, preview.Rows))
                return Changed(id);

            TempData["Success"] = T("Pré-visualização atualizada. Revê as diferenças antes de confirmar a importação.");

            return RedirectToAction(nameof(Preview), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(Guid id, Guid revision)
        {
            var draft = await imports.GetDraftAsync(id, Owner);

            if (draft == null)
                return Unavailable();

            if (draft.Revision != revision)
                return Changed(id);

            var preview = ExcelImportService.Preview(draft);

            if (!preview.CanConfirm)
                return Changed(id);

            try
            {
                if (await imports.ConfirmAsync(draft))
                {
                    TempData["Success"] = T("Importação concluída: {0} criados, {1} atualizados e {2} sem alterações.", preview.Plan.Count(x => x.Action == "Criar"), preview.Plan.Count(x => x.Action == "Atualizar"), preview.Plan.Count(x => x.Action == "Sem alterações"));

                    return RedirectToAction("Index", draft.ImportType == "families" ? "InvitationGroups" : "Guests");
                }
            }
            catch (Exception ex) when (ex is DbUpdateException or DbException)
            {
                db.ChangeTracker.Clear();
            }

            await imports.ReviewAsync(draft, preview.Rows);

            TempData["Error"] = T("A importação não foi concluída. Os dados podem ter mudado desde a pré-visualização. Revê a versão atualizada e confirma novamente.");

            return RedirectToAction(nameof(Preview), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(Guid id, Guid revision)
        {
            var draft = await imports.GetDraftAsync(id, Owner);

            if (draft == null)
                return Unavailable();

            if (draft.Revision != revision)
                return Changed(id);

            var deleted = await db.AdminImportDrafts.Where(x => x.Id == id && x.OwnerId == Owner && x.Revision == revision && x.CompletedAt == null).ExecuteDeleteAsync();

            if (deleted != 1)
                return Changed(id);

            TempData["Success"] = T("Importação cancelada. Nenhum registo foi alterado.");

            return RedirectToAction(draft.ImportType == "families" ? nameof(Families) : nameof(Guests));
        }

        [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(6 * 1024 * 1024)]
        public Task<IActionResult> ImportFamilies(ExcelImportViewModel model) => Upload("families", model.File);

        [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(6 * 1024 * 1024)]
        public Task<IActionResult> ImportGuests(ExcelImportViewModel model) => Upload("guests", model.File);

        #endregion

        #region Private Methods

        private async Task<IActionResult> Upload(string type, IFormFile? file)
        {
            var errors = new List<string>();
            var rows = ExcelImportService.ReadRows(file, type, errors);

            if (errors.Count > 0)
            {
                ViewBag.ImportErrors = errors;

                return View(ViewPath + "Index.cshtml", CreateViewModel(type));
            }

            var draft = await imports.CreateDraftAsync(type, Owner, rows);

            return RedirectToAction(nameof(Preview), new { id = draft.Id });
        }

        private IActionResult Changed(Guid id)
        {
            TempData["Error"] = T("Esta pré-visualização foi alterada ou contém erros. Revê a versão atual antes de continuar.");

            return RedirectToAction(nameof(Preview), new { id });
        }

        private IActionResult Unavailable()
        {
            Response.StatusCode = StatusCodes.Status410Gone;

            return View(ViewPath + "Unavailable.cshtml");
        }

        private static ExcelImportViewModel CreateViewModel(string type) => new()
        {
            ImportType = type,
            Title = T("Importar ") + ExcelImportService.Sheet(type),
            Description = T("Carrega o Excel, revê e corrige os dados, e só depois confirma as alterações."),
            TemplateAction = type == "families" ? nameof(DownloadFamiliesTemplate) : nameof(DownloadGuestsTemplate),
            SubmitAction = type == "families" ? nameof(ImportFamilies) : nameof(ImportGuests),
            ExpectedSheetName = ExcelImportService.Sheet(type), 
            Columns = ExcelImportService.Columns(type)
        };

        private static FileResult Template(string type)
        {
            using var book = new XLWorkbook();

            #region Instructions Sheet

            var instructions = book.Worksheets.Add(T("Instruções"));

            instructions.Cell("A1").Value = T("Importação com Pré-Visualização e Confirmação");
            instructions.Cell("A2").Value = T("ID vazio: novo registo. ID existente: atualizar esse registo. Consulta o ID na lista da administração.");
            instructions.Cell("A3").Value = type == "families" ? T("Uma referência existente também identifica a família a atualizar. Não repitas a referência ou o ID.") : T("Para atualizar convidados, indica o ID. Não são associados apenas pelo nome. A referência da família tem de existir.");
            instructions.Cell("A4").Value = T("Os campos opcionais vazios apagam o valor anterior nas atualizações. Revê a comparação antes de confirmar.");
            instructions.Cell("A5").Value = T("Até 500 linhas e 5 MB. Usa valores, não fórmulas. Booleanos: Sim ou Não. Não alteres os títulos nem a folha.");

            instructions.Column(1).Width = 110;
            instructions.Range("A1:A5").Style.Alignment.WrapText = true;
            instructions.Rows(1, 5).Height = 40;

            #endregion

            #region Data Sheet

            var sheet = book.Worksheets.Add(ExcelImportService.Sheet(type));
            var headers = ExcelImportService.Columns(type);

            for (var i = 0; i < headers.Length; i++)
                sheet.Cell(1, i + 1).Value = headers[i];

            var header = sheet.Range(1, 1, 1, headers.Length);

            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#BD4742");
            header.Style.Font.FontColor = XLColor.White;
            header.Style.Font.Bold = true;

            sheet.Columns(1, headers.Length).Width = 26;
            sheet.SheetView.FreezeRows(1);

            #endregion

            using var stream = new MemoryStream();

            book.SaveAs(stream);

            return new FileContentResult(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            {
                FileDownloadName = type == "families" ? "familias-template.xlsx" : "convidados-template.xlsx"
            };
        }

        #endregion
    }
}
