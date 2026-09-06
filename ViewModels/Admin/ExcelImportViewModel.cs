using Microsoft.AspNetCore.Http;

namespace CasamentoTatianaDiogo.ViewModels.Admin
{
    public class ExcelImportViewModel
    {
        public string ImportType { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string TemplateAction { get; init; } = string.Empty;
        public string SubmitAction { get; init; } = string.Empty;
        public string ExpectedSheetName { get; init; } = string.Empty;
        public IReadOnlyList<string> Columns { get; init; } = [];
        public IFormFile? File { get; set; }
    }
}
