using CasamentoTatianaDiogo.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace CasamentoTatianaDiogo.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LanguageController : Controller
{
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Change(string language, string returnUrl)
    {
        var culture = ApplicationLanguages.Normalize(language);
        if (culture == null || !Url.IsLocalUrl(returnUrl)) return BadRequest();
        var fragmentAt = returnUrl.IndexOf('#');
        var fragment = fragmentAt < 0 ? "" : returnUrl[fragmentAt..];
        var target = fragmentAt < 0 ? returnUrl : returnUrl[..fragmentAt];
        var queryAt = target.IndexOf('?');
        if (queryAt >= 0)
        {
            var query = QueryHelpers.ParseQuery(target[queryAt..]);
            query.Remove("culture");
            query.Remove("ui-culture");
            target = QueryHelpers.AddQueryString(target[..queryAt], query);
        }
        Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)), new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true,
                HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = Request.IsHttps,
                Path = Request.PathBase.HasValue ? Request.PathBase.Value : "/"
            });
        return LocalRedirect(target + fragment);
    }
}
