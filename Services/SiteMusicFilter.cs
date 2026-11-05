using CasamentoTatianaDiogo.Controllers;
using CasamentoTatianaDiogo.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CasamentoTatianaDiogo.Services;

// Keep the Spotify player in the outer document. Public pages, including RSVP POSTs,
// retain their native navigation, validation and scripts in a same-origin frame.
public sealed class SiteMusicFilter(IWeddingSettingsService settings) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (context.Controller is HomeController or InformationController or PhotosController or RsvpController &&
            context.Result is ViewResult view && context.HttpContext.Response.StatusCode == 200 &&
            HttpMethods.IsGet(request.Method) && !request.Query.ContainsKey("sitePage") &&
            request.Headers["Sec-Fetch-Dest"] == "document")
        {
            var track = SpotifyTrack.Normalize((await settings.GetAsync()).SpotifyTrackUrl);
            if (track != null)
            {
                view.ViewName = "~/Views/Shared/SiteMusic.cshtml";
                view.ViewData["SpotifyTrack"] = track;
                context.HttpContext.Response.Headers.CacheControl = "no-store";
            }
        }
        await next();
    }
}
