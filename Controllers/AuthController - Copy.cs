//using System.Security.Claims;
//using Microsoft.AspNetCore.Authentication;
//using Microsoft.AspNetCore.Authentication.Cookies;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using ARS.Classess.Utils;
//using ARS.Data;

//namespace ARS.Controllers
//{
//    public class AuthController : Controller
//    {
//        private readonly AppDbContext _db;
//        private readonly IConfiguration _config;

//        public AuthController(AppDbContext db, IConfiguration config)
//        {
//            _db = db;
//            _config = config;
//        }

//        // ── GET /Auth/Login ───────────────────────────────────────────────────
//        [HttpGet]
//        public IActionResult Login()
//        {
//            if (User.Identity?.IsAuthenticated == true)
//                return RedirectToAction("Index", "Dashboard");

//            return View();
//        }

//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> Login(string email, string password)
//        {
//            try
//            {
//                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
//                {
//                    ModelState.AddModelError("", "Email and password are required.");
//                    return View();
//                }

//                var user = await _db.AppUsers
//                    .FirstOrDefaultAsync(u => u.Email == email.Trim().ToLower());

//                if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.Password))
//                {
//                    ModelState.AddModelError("", "Invalid email or password.");
//                    return View();
//                }

//                if (!string.Equals(user.ProfileStatus, "enabled", StringComparison.OrdinalIgnoreCase))
//                {
//                    ModelState.AddModelError("", "Your account has been disabled. Please contact your administrator.");
//                    return View();
//                }

//                // ── Default-password check → force change ─────────────────────────
//                var defaultPassword = _config["DefaultPassword"];
//                if (!string.IsNullOrEmpty(defaultPassword) &&
//                    BCrypt.Net.BCrypt.Verify(defaultPassword, user.Password))
//                {
//                    TempData["ForceChangeUserId"] = user.Id;
//                    return RedirectToAction("UpdatePassword");
//                }

//                // ── All checks passed — sign the user in ──────────────────────────
//                await SignInUserAsync(user);

//                user.LastLoginDate = DateTime.UtcNow.ToString();
//                await _db.SaveChangesAsync();

//                await AuditLogger.LogAsync(
//                    db: _db,
//                    eventName: $"{email} - LOGIN SUCCESSFUL",
//                    userId: user.Id,
//                    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
//                    pageUrl: HttpContext.Request.Path
//                );

//                return RedirectToAction("Index", "Dashboard");
//            }
//            catch (Exception)
//            {
//                ModelState.AddModelError("", "Login failed. Please try again.");
//                return View();
//            }
//        }

//        // ── GET /Auth/UpdatePassword ──────────────────────────────────────────
//        [HttpGet]
//        public async Task<IActionResult> UpdatePassword()
//        {
//            if (TempData["ForceChangeUserId"] is null)
//                return RedirectToAction("Login");

//            TempData.Keep("ForceChangeUserId");

//            var userId = (int)TempData.Peek("ForceChangeUserId")!;
//            var user = await _db.AppUsers.FindAsync(userId);

//            if (user is null)
//                return RedirectToAction("Login");

//            ViewBag.UserEmail = user.Email;
//            return View();
//        }

//        // ── POST /Auth/UpdatePassword ─────────────────────────────────────────
//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> UpdatePassword(string oldPassword, string newPassword, string confirmPassword)
//        {
//            if (TempData["ForceChangeUserId"] is not int userId)
//                return RedirectToAction("Login");

//            TempData["ForceChangeUserId"] = userId;

//            var user = await _db.AppUsers.FindAsync(userId);
//            if (user is null)
//                return RedirectToAction("Login");

//            ViewBag.UserEmail = user.Email;

//            if (string.IsNullOrWhiteSpace(oldPassword) || !BCrypt.Net.BCrypt.Verify(oldPassword, user.Password))
//            {
//                ModelState.AddModelError("", "Current password is incorrect.");
//                return View();
//            }

//            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
//            {
//                ModelState.AddModelError("", "New password must be at least 8 characters.");
//                return View();
//            }

//            if (newPassword != confirmPassword)
//            {
//                ModelState.AddModelError("", "Passwords do not match.");
//                return View();
//            }

//            var defaultPassword = _config["DefaultPassword"];
//            if (!string.IsNullOrEmpty(defaultPassword) && newPassword == defaultPassword)
//            {
//                ModelState.AddModelError("", "You cannot reuse the temporary password. Please choose a new one.");
//                return View();
//            }

//            user.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
//            await _db.SaveChangesAsync();

//            await AuditLogger.LogAsync(
//                db: _db,
//                eventName: "PASSWORD UPDATED",
//                userId: user.Id,
//                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
//                pageUrl: HttpContext.Request.Path
//            ); 
            

//            bool emailConfirmed = false;
            
//            try
//            {

//                bool updated = await EmailSender.SendPasswordUpdatedEmail(
//        address: user.Email,
//        firstName: user.FirstName,
//        updatedDate: DateTime.Now.ToString("MMM dd, yyyy HH:mm"),
//        role: user.Role
//    );

//                emailConfirmed = true;
//            }
//            catch (Exception ex) {
//                emailConfirmed = false;
//            }

           
//            if (emailConfirmed)
//            {
//                TempData["SuccessMessage"] = "Password updated successfully. Please sign in with your new password.";
//            }
//            else
//            {
//                TempData["SuccessMessage"] = "Password updated successfully. No confirmation email sent";
//            }
//            return RedirectToAction("Login");
//        }

//        // ── GET /Auth/Logout ──────────────────────────────────────────────────
//        public async Task<IActionResult> Logout()
//        {
//            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
//            return RedirectToAction("Login");
//        }

//        // ── Private helper ────────────────────────────────────────────────────
//        private async Task SignInUserAsync(ARS.Models.AppUser user)
//        {
//            var claims = new List<Claim>
//            {
//                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
//                new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
//                new(ClaimTypes.Email, user.Email),
//                new(ClaimTypes.Role, user.Role ?? "Support"),
//                new("department", user.Department ?? ""),
//                new("firstName", user.FirstName ?? ""),
//                new("lastName", user.LastName ?? ""),
//                new("profileStatus", user.ProfileStatus ?? "enabled")
//            };

//            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
//            var principal = new ClaimsPrincipal(identity);

//            await HttpContext.SignInAsync(
//                CookieAuthenticationDefaults.AuthenticationScheme,
//                principal,
//                new AuthenticationProperties { IsPersistent = true });
//        }
//    }
//}
