using CasamentoTatianaDiogo.ViewModels;
using CasamentoTatianaDiogo.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.WebUtilities;

namespace CasamentoTatianaDiogo.Services;

public static class LanguageNavigation
{
    public static string ReturnUrl(ViewContext view, IUrlHelper url)
    {
        var request = view.HttpContext.Request;
        if (view.ViewData.Model is RsvpSelectionViewModel selection)
        {
            var target = url.Action("Select", "Rsvp", new { id = selection.Guest.Id, editSessionId = selection.Input.EditSessionId })!;
            return request.Query.ContainsKey("sitePage") ? QueryHelpers.AddQueryString(target, "sitePage", "1") : target;
        }
        if (HttpMethods.IsGet(request.Method)) return request.PathBase + request.Path + request.QueryString;
        if (view.ViewData.Model is RsvpContactViewModel contact) return url.Action("Contact", "Rsvp", new { id = contact.GuestId })!;
        if (view.ViewData.Model is TranslationsViewModel translations) return url.Action("Index", "Translations", new { language = translations.Language })!;
        if (view.ViewData.Model is ExcelImportViewModel import) return url.Action(import.ImportType == "families" ? "Families" : "Guests", "ExcelImport")!;
        var action = Path.GetFileNameWithoutExtension(view.View.Path);
        var controller = view.RouteData.Values["controller"]?.ToString();
        var id = view.ViewData.ModelExplorer.Properties.FirstOrDefault(x => x.Metadata.PropertyName == "Id")?.Model ?? view.RouteData.Values["id"];
        if (action == "Edit" && id is int number && number == 0 && controller is "Guests" or "InvitationGroups" or "FaqAdmin" or "Timeline") action = "Create";
        var page = (view.ViewData["MessagePage"] as WeddingPageMessages)?.Controller;
        return url.Action(action, controller, new { id, page }) ?? "/";
    }
}
