using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Models.Enums;
using CasamentoTatianaDiogo.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Controllers.Admin
{
    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]/{id?}")]
    public class DashboardController(ApplicationDbContext db) : Controller
    {
        public async Task<IActionResult> Index() => View("~/Views/Admin/Dashboard/Index.cshtml", new DashboardViewModel
        {
            Attending = await db.Guests.CountAsync(g => g.CurrentStatus == RsvpStatus.Attending),
            NotAttending = await db.Guests.CountAsync(g => g.CurrentStatus == RsvpStatus.NotAttending),
            Pending = await db.Guests.CountAsync(g => g.CurrentStatus == RsvpStatus.Pending),
            UploadedPhotos = await db.PhotoUploads.CountAsync(),
            GuestCount = await db.Guests.CountAsync()
        });
    }

    [Route("Admin/[action]")]
    public class AdminController(SignInManager<ApplicationUser> signInManager) : Controller
    {
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            return View("~/Views/Admin/Login.cshtml");
        }

        [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
        {
            var r = await signInManager.PasswordSignInAsync(email, password, false, true);

            if (r.Succeeded)
                return LocalRedirect(returnUrl ?? "Admin/Dashboard/Index");

            ModelState.AddModelError("", "Login inválido!");

            return View("~/Views/Admin/Login.cshtml");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await signInManager.SignOutAsync();

            return RedirectToAction("Index", "Home", new { area = "" });
        }

        [AllowAnonymous]
        public IActionResult AccessDenied() => View("~/Views/Admin/AccessDenied.cshtml");
    }
}
