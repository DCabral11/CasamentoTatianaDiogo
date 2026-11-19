using CasamentoTatianaDiogo.Controllers;
using CasamentoTatianaDiogo.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CasamentoTatianaDiogo.Services;

// Keep the Spotify player in the outer document. Public pages, including RSVP POSTs,
// retain their native navigation, validation and scripts in a same-origin frame.
public sealed class SiteMusicFilter(IWeddingSettingsService settings, FaqService faqs) : IAsyncActionFilter, IAsyncResultFilter
{
    private const string ShellView = "~/Views/Shared/SiteMusic.cshtml";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Entry pages render the shell before loading their page models. Deep RSVP
        // links keep their redirects, cookies and access checks before wrapping.
        if (context.Controller is Controller controller && CanWrap(context, controller) &&
            context.ActionDescriptor.RouteValues["action"] == "Index")
        {
            var track = SpotifyTrack.Normalize((await settings.GetPresentationAsync()).SpotifyTrackUrl);
            if (track != null && (controller is not FaqController || (await faqs.GetSettingsAsync()).IsEnabled))
            {
                controller.ViewData["SpotifyTrack"] = track;
                context.HttpContext.Response.Headers.CacheControl = "no-store";
                context.Result = controller.View(ShellView);
                return;
            }
        }
        await next();
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (CanWrap(context, context.Controller) && context.Result is ViewResult view &&
            view.ViewName != ShellView && context.HttpContext.Response.StatusCode == 200)
        {
            var track = SpotifyTrack.Normalize((await settings.GetPresentationAsync()).SpotifyTrackUrl);
            if (track != null)
            {
                view.ViewName = ShellView;
                view.ViewData["SpotifyTrack"] = track;
                context.HttpContext.Response.Headers.CacheControl = "no-store";
            }
        }
        await next();
    }

    private static bool CanWrap(FilterContext context, object controller)
    {
        var request = context.HttpContext.Request;
        return controller is HomeController or InformationController or PhotosController or RsvpController or FaqController &&
            (controller is RsvpController || context.ActionDescriptor.RouteValues["action"] == "Index") &&
            HttpMethods.IsGet(request.Method) && !request.Query.ContainsKey("sitePage") &&
            request.Headers["Sec-Fetch-Dest"] == "document";
    }
}
