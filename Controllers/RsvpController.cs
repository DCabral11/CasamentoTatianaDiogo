using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CasamentoTatianaDiogo.Controllers
{
    public class RsvpController(IRsvpService rsvp) : Controller
    {
        public IActionResult Index() => View(new RsvpSearchViewModel());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Search(RsvpSearchViewModel model)
        {
            if (!ModelState.IsValid)
                return View("Index", model);

            model.Results = await rsvp.SearchGuestsAsync(model.Query);

            if (!model.Results.Any())
                model.Message = "Não foram encontrados convidados. Por favor tenta outro nome ou contacta os noivos!";

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
            TempData[result.ok ? "Success" : "Error"] = result.message;

            return result.ok ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Select), new
            {
                id = model.GuestId
            });
        }
    }
}
