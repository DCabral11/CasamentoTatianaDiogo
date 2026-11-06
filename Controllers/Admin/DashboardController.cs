#region References

using CasamentoTatianaDiogo.Common.Errors;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Models.Enums;
using CasamentoTatianaDiogo.Services;
using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

#endregion

namespace CasamentoTatianaDiogo.Controllers.Admin
{

    [Authorize(Roles = "Admin")]
    [Route("Admin/[controller]/[action]/{id?}")]
    public class DashboardController(ApplicationDbContext db, WeddingClock clock) : Controller
    {
        /// <summary>
        /// Loads the dashboard page with the current wedding countdown information and other relevant information for the admin.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var guests = await db.Guests.GroupBy(g => g.CurrentStatus).Select(group => new { Status = group.Key, Count = group.Count() }).ToDictionaryAsync(row => row.Status, row => row.Count);
            var budget = await db.BudgetLines.GroupBy(_ => 1).Select(lines => new
            {
                Balance = lines.Sum(line => (line.RealCost ?? 0m) - line.Paid),
                Unpriced = lines.Count(line => line.RealCost == null)
            }).SingleOrDefaultAsync();
            var next = await db.BudgetLines.Where(line => line.NextPayment.HasValue && (line.RealCost == null || line.Paid < line.RealCost)).OrderBy(line => line.NextPayment).ThenBy(line => line.Id).Select(line => new
            {
                line.Id,
                Date = line.NextPayment!.Value,
                Category = line.Category!.Name
            }).FirstOrDefaultAsync();

            return View("~/Views/Admin/Dashboard/Index.cshtml", new DashboardViewModel
            {
                GuestCount = guests.Values.Sum(),
                Attending = guests.GetValueOrDefault(RsvpStatus.Attending),
                NotAttending = guests.GetValueOrDefault(RsvpStatus.NotAttending),
                Pending = guests.GetValueOrDefault(RsvpStatus.Pending),
                BudgetBalance = budget?.Balance ?? 0m,
                UnpricedLines = budget?.Unpriced ?? 0,
                PaymentLineId = next?.Id,
                PaymentDate = next?.Date,
                PaymentCategory = next?.Category,
                IsPaymentOverdue = next?.Date < DateOnly.FromDateTime(clock.Now),
                MissingSongs = await db.MusicMoments.CountAsync(moment => moment.Song == null || moment.Song == ""),
                UploadedPhotos = await db.PhotoUploads.CountAsync()
            });
        }
    }

    [Route("Admin/[action]")]
    public class AdminController(SignInManager<ApplicationUser> signInManager, IAppMessageService messages) : Controller
    {
        /// <summary>
        /// Loads the login page for the admin section. 
        /// If a return URL is provided, it will be passed to the view for redirection after successful login.
        /// </summary>
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            return View("~/Views/Admin/Login.cshtml");
        }

        /// <summary>
        /// Handles the login request for the admin section.
        /// </summary>
        [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
        {
            var r = await signInManager.PasswordSignInAsync(email, password, false, true);

            if (r.Succeeded)
                return LocalRedirect(returnUrl ?? "/Admin/Dashboard/Index");

            ModelState.AddModelError("", messages.Get(ErrorCode.AdminLoginFailed));

            return View("~/Views/Admin/Login.cshtml");
        }

        /// <summary>
        /// Handles the logout request for the admin section, signing out the current user and redirecting to the home page.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await signInManager.SignOutAsync();

            return RedirectToAction("Index", "Home", new { area = "" });
        }

        /// <summary>
        /// Handles the access denied scenario for the admin section, returning a view that informs the user they do not have permission to access the requested resource.
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        public IActionResult AccessDenied() => View("~/Views/Admin/AccessDenied.cshtml");
    }
}