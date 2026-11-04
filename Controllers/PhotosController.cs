using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.AspNetCore.Mvc;
using CasamentoTatianaDiogo.Services;

namespace CasamentoTatianaDiogo.Controllers
{
    public class PhotosController(IPhotoUploadService photos, ResumablePhotoUploadService uploads, ILogger<PhotosController> logger) : Controller
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

        [HttpPost, RequestSizeLimit(4 * 1024 * 1024), ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(PhotoUploadViewModel vm, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Revê os dados das fotografias e tenta novamente.";
                return RedirectToAction(nameof(Index));
            }
            var result = await photos.UploadAsync(vm, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
            TempData[result.ok ? "Success" : "Error"] = result.message;

            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(16 * 1024)]
        public async Task<IActionResult> BeginUpload([FromBody] BeginPhotoUploadRequest request, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { message = "Revê o nome, formato e tamanho da fotografia." });
            try { return Ok(await uploads.BeginAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct)); }
            catch (PhotoUploadValidationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Could not start a photo upload.");
                return StatusCode(503, new { message = "Não foi possível iniciar o envio. Tenta novamente mais tarde." });
            }
        }

        [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(ResumablePhotoUploadService.ChunkSize)]
        public async Task<IActionResult> UploadChunk([FromHeader(Name = "X-Upload-Token")] string token,
            [FromHeader(Name = "X-Upload-Offset")] long offset, CancellationToken ct)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(token) || Request.ContentLength is not > 0 or > ResumablePhotoUploadService.ChunkSize)
                return BadRequest(new { message = "O envio da fotografia é inválido." });
            var bytes = new byte[(int)Request.ContentLength.Value];
            await Request.Body.ReadExactlyAsync(bytes, ct);
            try { return Ok(await uploads.UploadChunkAsync(token, offset, bytes, ct)); }
            catch (PhotoUploadValidationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Could not upload a photo chunk.");
                return StatusCode(503, new { message = "O envio foi interrompido. Tenta novamente dentro de momentos." });
            }
        }
    }
}
