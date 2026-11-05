using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Wedmanga.Data;
using Wedmanga.Models.Entities;

namespace Wedmanga.Controllers
{
    public class AccountController : Controller
    {
        private readonly MangaDbContext _context;

        public AccountController(MangaDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // ĐĂNG NHẬP
        // =====================================================

        [Route("/tai-khoan/dang-nhap", Name = "login")]
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Profile", "Account");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("", "Vui lòng nhập đầy đủ Email và Mật khẩu.");
                return View();
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => (u.Email == email || u.Username == email) && !u.IsDeleted);

            if (user == null)
            {
                ModelState.AddModelError("", "Tài khoản không tồn tại.");
                return View();
            }

            if (user.Status != "Active")
            {
                ModelState.AddModelError("", "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ admin.");
                return View();
            }

            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                ModelState.AddModelError("", "Sai mật khẩu.");
                return View();
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role.RoleName),
                new("Avatar", user.Avatar ?? "/images/default-avatar.png")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
                new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7) });

            if (user.Role.RoleName is "Admin" or "Moderator")
                return RedirectToAction("Index", "Admin");

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        // =====================================================
        // ĐĂNG KÝ
        // =====================================================

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string username, string email, string password, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("", "Vui lòng điền đầy đủ thông tin.");
                return View();
            }

            if (password != confirmPassword)
            {
                ModelState.AddModelError("", "Mật khẩu xác nhận không khớp.");
                return View();
            }

            if (password.Length < 6)
            {
                ModelState.AddModelError("", "Mật khẩu phải có ít nhất 6 ký tự.");
                return View();
            }

            if (await _context.Users.AnyAsync(u => u.Email == email))
            {
                ModelState.AddModelError("", "Email này đã được đăng ký.");
                return View();
            }

            if (await _context.Users.AnyAsync(u => u.Username == username))
            {
                ModelState.AddModelError("", "Tên người dùng này đã tồn tại.");
                return View();
            }

            var userRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "User");
            if (userRole == null)
            {
                ModelState.AddModelError("", "Lỗi hệ thống: Không tìm thấy vai trò mặc định.");
                return View();
            }

            var newUser = new User
            {
                Username = username.Trim(),
                Email = email.Trim().ToLower(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                RoleId = userRole.RoleId,
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đăng ký thành công! Vui lòng đăng nhập.";
            return RedirectToAction("Login");
        }

        // =====================================================
        // ĐĂNG XUẤT
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            Response.Cookies.Delete("Username");
            return RedirectToAction("Index", "Home");
        }

        // =====================================================
        // HỒ SƠ CÁ NHÂN
        // =====================================================

        [Route("/tai-khoan/ho-so", Name = "profile")]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            if (User.Identity?.IsAuthenticated != true)
                return RedirectToAction("Login");

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
                return RedirectToAction("Login");

            var user = await _context.Users
                .Where(u => u.UserId == userId && !u.IsDeleted)
                .Select(u => new
                {
                    u.UserId, u.Username, u.Email, u.Avatar,
                    u.Status, u.CreatedAt,
                    RoleName = u.Role.RoleName,
                    BookmarkCount = u.Bookmarks.Count,
                    CommentCount = u.Comments.Count(c => !c.IsDeleted),
                    RecentHistory = u.ReadingHistories
                        .OrderByDescending(h => h.UpdatedAt)
                        .Take(5)
                        .Select(h => new { h.Story.Title, h.Story.Slug, h.Story.CoverImage, h.UpdatedAt })
                })
                .FirstOrDefaultAsync();

            if (user == null) return NotFound();

            ViewBag.UserProfile = user;
            return View();
        }

        // =====================================================
        // ĐỔI MẬT KHẨU
        // =====================================================

        [HttpGet]
        public IActionResult ChangePassword()
        {
            if (User.Identity?.IsAuthenticated != true)
                return RedirectToAction("Login");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmNewPassword)
        {
            if (User.Identity?.IsAuthenticated != true)
                return RedirectToAction("Login");

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
            var user = await _context.Users.FindAsync(userId);

            if (user == null) return NotFound();

            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                ModelState.AddModelError("", "Mật khẩu hiện tại không đúng.");
                return View();
            }

            if (newPassword != confirmNewPassword)
            {
                ModelState.AddModelError("", "Mật khẩu mới xác nhận không khớp.");
                return View();
            }

            if (newPassword.Length < 6)
            {
                ModelState.AddModelError("", "Mật khẩu mới phải có ít nhất 6 ký tự.");
                return View();
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
            return RedirectToAction("Profile");
        }

        // =====================================================
        // TÌM KIẾM TRUYỆN
        // =====================================================

        public async Task<IActionResult> Search(string q, int page = 1)
        {
            if (string.IsNullOrWhiteSpace(q))
                return RedirectToAction("Index", "Home");

            const int pageSize = 20;
            var keyword = q.Trim().ToLower();

            var total = await _context.Stories
                .CountAsync(s => !s.IsDeleted &&
                    (s.Title.ToLower().Contains(keyword) || (s.Author != null && s.Author.ToLower().Contains(keyword))));

            var results = await _context.Stories
                .Where(s => !s.IsDeleted &&
                    (s.Title.ToLower().Contains(keyword) || (s.Author != null && s.Author.ToLower().Contains(keyword))))
                .OrderByDescending(s => s.ViewCount)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new { s.StoryId, s.Title, s.Slug, s.CoverImage, s.Author, s.RatingAvg, s.ViewCount })
                .ToListAsync();

            ViewBag.Query = q;
            ViewBag.Results = results;
            ViewBag.Total = total;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);

            return View();
        }
    }
}
