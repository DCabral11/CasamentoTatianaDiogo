using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.ViewModels;
using CasamentoTatianaDiogo.Common.Errors;
using Microsoft.AspNetCore.Mvc;

namespace CasamentoTatianaDiogo.Controllers
{
    public class RsvpController(IRsvpService rsvp, IAppMessageService messages) : Controller
    {
        public IActionResult Index(bool submitted = false)
        {
            ViewData["RsvpJustSubmitted"] = submitted;
            return View(new RsvpSearchViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Search(RsvpSearchViewModel model)
        {
            if (!ModelState.IsValid)
                return View("Index", model);

            model.Results = await rsvp.SearchGuestsAsync(model.Query);

            if (!model.Results.Any())
                model.Message = messages.Get(ErrorCode.RsvpSearchNoResults);

            return View("Index", model);
        }

        public async Task<IActionResult> Select(int id)
        {
            var vm = await rsvp.GetSelectionAsync(id);

            if (vm == null)
                return NotFound();

            return View("Select", vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(RsvpSubmitViewModel model)
        {
            if (!ModelState.IsValid)
                return RedirectToAction(nameof(Select), new
                {
                    id = model.GuestId
                });

            var result = await rsvp.SubmitAsync(model, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
            TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
                ? messages.Get(ErrorCode.RsvpSaved)
                : result.Message;

            return result.Succeeded ? RedirectToAction(nameof(Index), new { submitted = true }) : RedirectToAction(nameof(Select), new
            {
                id = model.GuestId
            });
        }
    }
}
