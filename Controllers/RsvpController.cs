using CasamentoTatianaDiogo.Services;
using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.ViewModels;
using CasamentoTatianaDiogo.Common.Errors;
using Microsoft.AspNetCore.Mvc;
using CasamentoTatianaDiogo.Models;

namespace CasamentoTatianaDiogo.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class RsvpController(IRsvpService rsvp, IAppMessageService messages,
    RsvpVerificationService verification, IWebHostEnvironment environment) : Controller
{
    private const string ChallengeCookie = "Casamento.RsvpChallenge";
    private const string AccessCookie = "Casamento.RsvpAccess";
    private string? AccessToken => Request.Cookies[AccessCookie];

    public IActionResult Index(bool submitted = false)
    {
        ViewData["RsvpJustSubmitted"] = submitted;
        return View(new RsvpSearchViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Search(RsvpSearchViewModel model)
    {
        if (!ModelState.IsValid) return View("Index", model);
        model.Results = await rsvp.SearchGuestsAsync(model.Query);
        if (!model.Results.Any()) model.Message = messages.Get(ErrorCode.RsvpSearchNoResults);
        return View("Index", model);
    }

    [HttpGet]
    public async Task<IActionResult> Contact(int id)
    {
        var model = await verification.GetContactFormAsync(id);
        return model == null ? NotFound() : View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SendCode(RsvpContactViewModel model, CancellationToken cancellationToken)
    {
        var form = await verification.GetContactFormAsync(model.GuestId);
        if (form == null) return NotFound();
        form.Contact = model.Contact;
        if (!ModelState.IsValid) return View("Contact", form);
        try
        {
            var token = await verification.RequestCodeAsync(model.GuestId, RsvpVerificationChannel.Email, model.Contact,
                HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
            Response.Cookies.Append(ChallengeCookie, token, CookieOptions(TimeSpan.FromMinutes(10)));
            Response.Cookies.Delete(AccessCookie, CookieOptions(null));
            return RedirectToAction(nameof(Verify));
        }
        catch (RsvpVerificationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("Contact", form);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Verify()
    {
        var form = await verification.GetCodeFormAsync(Request.Cookies[ChallengeCookie]);
        if (form != null) return View(form);
        TempData["Error"] = RsvpVerificationService.InvalidCode;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify(RsvpCodeViewModel model)
    {
        var form = await verification.GetCodeFormAsync(Request.Cookies[ChallengeCookie]);
        if (form == null)
        {
            TempData["Error"] = RsvpVerificationService.InvalidCode;
            return RedirectToAction(nameof(Index));
        }
        try
        {
            var ticket = await verification.VerifyCodeAsync(Request.Cookies[ChallengeCookie], model.Code);
            Response.Cookies.Append(AccessCookie, ticket.Token, CookieOptions(TimeSpan.FromMinutes(30)));
            Response.Cookies.Delete(ChallengeCookie, CookieOptions(null));
            return RedirectToAction(nameof(Select), new { id = ticket.GuestId });
        }
        catch (RsvpVerificationException exception)
        {
            ModelState.Clear();
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(form);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Select(int id)
    {
        var model = await rsvp.GetSelectionAsync(id, AccessToken);
        return model == null ? RedirectToAction(nameof(Contact), new { id }) : View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(RsvpSubmitViewModel model)
    {
        if (await verification.GetAccessAsync(AccessToken, model.GuestId) == null)
        {
            TempData["Error"] = "Verifica o teu contacto antes de confirmar ou alterar a resposta.";
            return RedirectToAction(nameof(Contact), new { id = model.GuestId });
        }
        if (ModelState.IsValid)
        {
            var result = await rsvp.SubmitAsync(model, AccessToken, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
            if (result.Succeeded)
            {
                Response.Cookies.Delete(AccessCookie, CookieOptions(null));
                TempData["Success"] = messages.Get(ErrorCode.RsvpSaved);
                return RedirectToAction(nameof(Index), new { submitted = true });
            }
            ModelState.AddModelError(string.Empty, result.Message);
        }
        var form = await rsvp.GetSelectionAsync(model.GuestId, AccessToken);
        if (form == null) return RedirectToAction(nameof(Contact), new { id = model.GuestId });
        form.Input = model;
        return View("Select", form);
    }

    private CookieOptions CookieOptions(TimeSpan? lifetime) => new()
    {
        HttpOnly = true, Secure = !environment.IsDevelopment(), SameSite = SameSiteMode.Lax,
        Path = "/", MaxAge = lifetime, IsEssential = true
    };
}
