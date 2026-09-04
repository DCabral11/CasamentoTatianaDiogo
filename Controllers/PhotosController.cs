using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CasamentoTatianaDiogo.Controllers
{
    public class PhotosController(IPhotoUploadService photos) : Controller
    {
        public async Task<IActionResult> Index()
        {
            var window = await photos.GetWindowAsync();

            return View(new PhotoUploadViewModel
            {
                IsOpen = window.open,
                Message = window.message,
                Settings = window.settings
            });
        }

        [HttpPost, RequestSizeLimit(104857600), ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(PhotoUploadViewModel vm, CancellationToken ct)
        {
            var result = await photos.UploadAsync(vm, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
            TempData[result.ok ? "Success" : "Error"] = result.message;

            return RedirectToAction(nameof(Index));
        }
    }
}
