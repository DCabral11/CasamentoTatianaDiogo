using CasamentoTatianaDiogo.Services;
using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.ViewModels;
using CasamentoTatianaDiogo.Common.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using CasamentoTatianaDiogo.Models;

namespace CasamentoTatianaDiogo.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class RsvpController(IRsvpService rsvp, IAppMessageService messages,
    RsvpVerificationService verification, IWebHostEnvironment environment,
    IWeddingSettingsService settings, WeddingClock clock) : Controller
{
    private const string ChallengeCookie = "Casamento.RsvpChallenge";
    private const string AccessCookie = "Casamento.RsvpAccess";
    private const string ReceiptCookie = "Casamento.RsvpReceipt";
    private string? AccessToken => Request.Cookies[AccessCookie];

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Reading an already saved receipt remains possible after the RSVP deadline.
        if (string.Equals(context.ActionDescriptor.RouteValues["action"], nameof(Receipt), StringComparison.Ordinal))
        {
            await next();
            return;
        }
        var deadline = (await settings.GetAsync()).RsvpDeadline;
        if (deadline.HasValue && DateOnly.FromDateTime(clock.Now) > deadline.Value)
        {
            Response.Headers.CacheControl = "no-store";
            // Apply the same cutoff to searches, code requests, verification and submission,
            // including forms that were already open when the deadline passed.
            context.Result = View("Closed", deadline.Value);
            return;
        }
        await next();
    }

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
    public async Task<IActionResult> Contact(int id, bool changeContact = false)
    {
        var model = await verification.GetContactFormAsync(id);
        if (model != null && !changeContact)
            model.PendingCode = await PendingCodeForAsync(id);
        return model == null ? NotFound() : View(model);
    }

    // A refresh/bookmark of the completed code-request page can resume safely by GET.
    [HttpGet]
    public IActionResult SendCode() => RedirectToAction(nameof(Verify));

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
            form.PendingCode = await verification.GetCodeFormAsync(token);
            ModelState.Clear();
            ViewData["RsvpCodeSent"] = true;
            return View("Contact", form);
        }
        catch (RsvpVerificationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            form.PendingCode = await PendingCodeForAsync(model.GuestId);
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
        // Bind the code to the request displayed in this form, even if another tab
        // requested a code for a different invitation and replaced the cookie.
        var token = model.ChallengeToken ?? Request.Cookies[ChallengeCookie];
        var form = await verification.GetCodeFormAsync(token);
        if (form == null)
        {
            TempData["Error"] = RsvpVerificationService.InvalidCode;
            return RedirectToAction(nameof(Index));
        }
        try
        {
            var ticket = await verification.VerifyCodeAsync(token, model.Code);
            Response.Cookies.Append(AccessCookie, ticket.Token, CookieOptions(TimeSpan.FromMinutes(30)));
            Response.Cookies.Delete(ChallengeCookie, CookieOptions(null));
            return RedirectToAction(nameof(Select), new { id = ticket.GuestId });
        }
        catch (RsvpVerificationException exception)
        {
            ModelState.Clear();
            ModelState.AddModelError(nameof(model.Code), exception.Message);
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
                var receiptToken = await verification.CreateReceiptTokenAsync(AccessToken);
                if (receiptToken != null)
                    Response.Cookies.Append(ReceiptCookie, receiptToken, CookieOptions(TimeSpan.FromHours(24)));
                else
                    Response.Cookies.Delete(ReceiptCookie, CookieOptions(null));
                Response.Cookies.Delete(AccessCookie, CookieOptions(null));
                return RedirectToAction(nameof(Receipt));
            }
            ModelState.AddModelError(result.Error?.Field ?? string.Empty, result.Message);
            if (result.Error?.Code == ErrorCode.RsvpConcurrencyConflict)
                Response.StatusCode = StatusCodes.Status409Conflict;
        }
        var form = await rsvp.GetSelectionAsync(model.GuestId, AccessToken, model.EditSessionId);
        if (form == null) return RedirectToAction(nameof(Contact), new { id = model.GuestId });
        form.Input = model;
        return View("Select", form);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditStatus(int guestId, Guid editSessionId)
    {
        var status = await rsvp.HeartbeatAsync(guestId, editSessionId, AccessToken);
        return status == null ? Unauthorized() : Json(status);
    }

    [HttpGet]
    public async Task<IActionResult> Receipt()
    {
        Response.Headers.CacheControl = "no-store";
        var receipt = await verification.GetReceiptAsync(Request.Cookies[ReceiptCookie]);
        if (receipt != null) return View(receipt);
        Response.StatusCode = StatusCodes.Status410Gone;
        return View("ReceiptUnavailable");
    }

    private CookieOptions CookieOptions(TimeSpan? lifetime) => new()
    {
        HttpOnly = true, Secure = !environment.IsDevelopment(), SameSite = SameSiteMode.Lax,
        Path = "/", MaxAge = lifetime, IsEssential = true
    };

    private async Task<RsvpCodeViewModel?> PendingCodeForAsync(int guestId)
    {
        var pending = await verification.GetCodeFormAsync(Request.Cookies[ChallengeCookie]);
        return pending?.GuestId == guestId ? pending : null;
    }
}
