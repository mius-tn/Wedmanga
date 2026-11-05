using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Wedmanga.Data;
using Wedmanga.Models;

namespace Wedmanga.Controllers
{
    [Authorize(Roles = "Admin,Moderator")]
    public class AdminController : Controller
    {
        private readonly MangaDbContext _context;

        public AdminController(MangaDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // DASHBOARD - Tổng quan hệ thống
        // =====================================================

        public async Task<IActionResult> Index()
        {
            // KPI Cards
            ViewBag.TotalStories = await _context.Stories.CountAsync(s => !s.IsDeleted);
            ViewBag.TotalChapters = await _context.Chapters.CountAsync(c => !c.IsDeleted);
            ViewBag.TotalUsers = await _context.Users.CountAsync(u => !u.IsDeleted);
            ViewBag.TotalViewsToday = await _context.Stories.SumAsync(s => s.ViewCount); // Tổng lượt xem

            // Dữ liệu biểu đồ: Top 5 truyện nhiều view nhất
            var topStories = await _context.Stories
                .Where(s => !s.IsDeleted)
                .OrderByDescending(s => s.ViewCount)
                .Take(5)
                .Select(s => new { s.Title, s.ViewCount, s.RatingAvg })
                .ToListAsync();

            // Bình luận gần đây
            var recentComments = await _context.Comments
                .Where(c => !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAt)
                .Take(5)
                .Select(c => new { c.Content, c.CreatedAt, c.User.Username, c.Story.Title })
                .ToListAsync();

            ViewBag.TopStories = topStories;
            ViewBag.RecentComments = recentComments;

            return View();
        }

        // =====================================================
        // QUẢN LÝ TRUYỆN
        // =====================================================

        public async Task<IActionResult> Stories(string? search, string? status, int page = 1)
        {
            const int pageSize = 20;
            var query = _context.Stories.Where(s => !s.IsDeleted).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(s => s.Title.Contains(search) || (s.Author != null && s.Author.Contains(search)));

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(s => s.Status == status);

            var total = await query.CountAsync();

            var stories = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new
                {
                    s.StoryId, s.Title, s.Author, s.Status,
                    s.ViewCount, s.RatingAvg, s.CreatedAt, s.CoverImage,
                    ChapterCount = s.Chapters.Count(c => !c.IsDeleted)
                })
                .ToListAsync();

            ViewBag.Stories = stories;
            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);

            return View();
        }

        public async Task<IActionResult> AddStory()
        {
            // Load danh sách thể loại cho multi-select
            var categories = await _context.Categories
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Name)
                .Select(c => new { c.CategoryId, c.Name })
                .ToListAsync();

            ViewBag.Categories = categories;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStory(
            string title, string alternateName, string author, string description, 
            int[] selectedCategories, IFormFile coverImage, string status, string type,
            int chapterCount, int viewCount, double ratingAvg)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(author) || coverImage == null || coverImage.Length == 0)
            {
                ModelState.AddModelError("", "Vui lòng nhập đầy đủ thông tin bắt buộc và tải lên ảnh trang bìa.");
                // Reload categories on error
                ViewBag.Categories = await _context.Categories.Where(c => !c.IsDeleted).OrderBy(c => c.Name).Select(c => new { c.CategoryId, c.Name }).ToListAsync();
                return View();
            }

            // Get Current User Info
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int adminUserId))
            {
                return Unauthorized();
            }
            var adminRoleName = User.FindFirstValue(ClaimTypes.Role) ?? "Moderator";

            // Sinh Slug từ Title
            var slug = GenerateSlug(title);

            // Kiểm tra trùng Slug (Optional now since SP handles it, but good for UI feedback)
            if (await _context.Stories.AnyAsync(s => s.Slug == slug))
            {
                slug += "-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }

            // Xử lý upload ảnh
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "covers");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(coverImage.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await coverImage.CopyToAsync(fileStream);
            }

            var coverImageUrl = "/images/covers/" + uniqueFileName;
            var categoryIdsStr = selectedCategories != null ? string.Join(",", selectedCategories) : "";

            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_Admin_CreateStory @AdminUserId, @AdminRoleName, @Title, @Slug, @Description, @Author, @CoverImage, @Status, @ChapterCount, @ViewCount, @RatingAvg, @CategoryIds",
                    new SqlParameter("@AdminUserId", adminUserId),
                    new SqlParameter("@AdminRoleName", adminRoleName),
                    new SqlParameter("@Title", title.Trim()),
                    new SqlParameter("@Slug", slug),
                    new SqlParameter("@Description", description?.Trim() ?? (object)DBNull.Value),
                    new SqlParameter("@Author", author.Trim() ?? (object)DBNull.Value),
                    new SqlParameter("@CoverImage", coverImageUrl),
                    new SqlParameter("@Status", status ?? "Ongoing"),
                    new SqlParameter("@Type", type ?? "Manga"),
                    new SqlParameter("@ChapterCount", chapterCount),
                    new SqlParameter("@ViewCount", viewCount),
                    new SqlParameter("@RatingAvg", ratingAvg),
                    new SqlParameter("@CategoryIds", categoryIdsStr)
                );

                TempData["SuccessMessage"] = "Thêm truyện mới thành công!";
                return RedirectToAction("Stories");
            }
            catch (SqlException ex)
            {
                ModelState.AddModelError("", "Lỗi CSDL: " + ex.Message);
                ViewBag.Categories = await _context.Categories.Where(c => !c.IsDeleted).OrderBy(c => c.Name).Select(c => new { c.CategoryId, c.Name }).ToListAsync();
                return View();
            }
        }

        // Hàm hỗ trợ tạo Slug đơn giản
        private string GenerateSlug(string phrase)
        {
            var s = phrase.ToLowerInvariant();
            s = System.Text.RegularExpressions.Regex.Replace(s, @"[^a-z0-9\s-]", "");
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", "-").Trim();
            return s;
        }

        [HttpGet]
        public async Task<IActionResult> EditStory(int id)
        {
            var story = await _context.Stories
                .Include(s => s.StoryCategories)
                .FirstOrDefaultAsync(s => s.StoryId == id && !s.IsDeleted);

            if (story == null) return NotFound();

            var categories = await _context.Categories
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Name)
                .Select(c => new { c.CategoryId, c.Name })
                .ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.SelectedCategories = story.StoryCategories.Select(sc => sc.CategoryId).ToList();

            return View(story);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStory(
            int storyId, string title, string alternateName, string author, string description, 
            int[] selectedCategories, IFormFile? coverImage, string status, string type,
            int viewCount, double ratingAvg, string currentCover)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(author))
            {
                ModelState.AddModelError("", "Vui lòng nhập đầy đủ Tên truyện và Tác giả.");
                ViewBag.Categories = await _context.Categories.Where(c => !c.IsDeleted).OrderBy(c => c.Name).Select(c => new { c.CategoryId, c.Name }).ToListAsync();
                return View(); // Trả về lại view hiện tại
            }

            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int adminUserId)) return Unauthorized();
            var adminRoleName = User.FindFirstValue(ClaimTypes.Role) ?? "Moderator";
            var slug = GenerateSlug(title);

            // Thumbnail Upload
            var finalCoverUrl = currentCover;
            if (coverImage != null && coverImage.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "covers");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(coverImage.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await coverImage.CopyToAsync(fileStream);
                }
                finalCoverUrl = "/images/covers/" + uniqueFileName;
            }

            var categoryIdsStr = selectedCategories != null ? string.Join(",", selectedCategories) : "";

            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_Admin_UpdateStory @AdminUserId, @AdminRoleName, @StoryId, @Title, @Slug, @Description, @Author, @CoverImage, @Status, @ViewCount, @RatingAvg, @CategoryIds",
                    new SqlParameter("@AdminUserId", adminUserId),
                    new SqlParameter("@AdminRoleName", adminRoleName),
                    new SqlParameter("@StoryId", storyId),
                    new SqlParameter("@Title", title.Trim()),
                    new SqlParameter("@Slug", slug),
                    new SqlParameter("@Description", description?.Trim() ?? (object)DBNull.Value),
                    new SqlParameter("@Author", author.Trim() ?? (object)DBNull.Value),
                    new SqlParameter("@CoverImage", finalCoverUrl ?? (object)DBNull.Value),
                    new SqlParameter("@Status", status ?? "Ongoing"),
                    new SqlParameter("@Type", type ?? "Manga"),
                    new SqlParameter("@ViewCount", viewCount),
                    new SqlParameter("@RatingAvg", ratingAvg),
                    new SqlParameter("@CategoryIds", categoryIdsStr)
                );

                TempData["SuccessMessage"] = "Cập nhật truyện thành công!";
                return RedirectToAction("Stories");
            }
            catch (SqlException ex)
            {
                ModelState.AddModelError("", "Lỗi CSDL: " + ex.Message);
                ViewBag.Categories = await _context.Categories.Where(c => !c.IsDeleted).Select(c => new { c.CategoryId, c.Name }).ToListAsync();
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStory(int storyId)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int adminUserId)) return Unauthorized();
            var adminRoleName = User.FindFirstValue(ClaimTypes.Role) ?? "Moderator";

            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_Admin_DeleteStory @AdminUserId, @AdminRoleName, @StoryId",
                    new SqlParameter("@AdminUserId", adminUserId),
                    new SqlParameter("@AdminRoleName", adminRoleName),
                    new SqlParameter("@StoryId", storyId)
                );
                TempData["SuccessMessage"] = "Đã xóa truyện thành công (Soft Delete)!";
            }
            catch (SqlException ex)
            {
                TempData["ErrorMessage"] = "Không thể xóa: " + ex.Message;
            }

            return RedirectToAction("Stories");
        }

        // =====================================================
        // QUẢN LÝ CHƯƠNG
        // =====================================================

        public async Task<IActionResult> Chapters(int? storyId, string? search, int page = 1)
        {
            const int pageSize = 30;

            if (!storyId.HasValue)
            {
                var queryStories = _context.Stories.Where(s => !s.IsDeleted).AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                    queryStories = queryStories.Where(s => s.Title.Contains(search) || (s.Author != null && s.Author.Contains(search)));

                var totalStories = await queryStories.CountAsync();

                var stories = await queryStories
                    .OrderByDescending(s => s.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(s => new
                    {
                        s.StoryId, s.Title, s.Author, s.Status,
                        s.ViewCount, s.RatingAvg, s.CreatedAt, s.CoverImage,
                        ChapterCount = s.Chapters.Count(c => !c.IsDeleted)
                    })
                    .ToListAsync();

                ViewBag.Stories = stories;
                ViewBag.Search = search;
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = (int)Math.Ceiling((double)totalStories / pageSize);
                ViewBag.IsStoryList = true;

                return View();
            }

            var query = _context.Chapters.Where(c => !c.IsDeleted && c.StoryId == storyId.Value).AsQueryable();

            var total = await query.CountAsync();

            var chapters = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    c.ChapterId, c.ChapterNumber, c.Title, c.ViewCount, c.CreatedAt,
                    StoryTitle = c.Story.Title, c.StoryId
                })
                .ToListAsync();

            // Danh sách truyện để filter
            var allStories = await _context.Stories
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.Title)
                .Select(s => new { s.StoryId, s.Title })
                .ToListAsync();

            ViewBag.Chapters = chapters;
            ViewBag.AllStories = allStories;
            ViewBag.FilterStoryId = storyId;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
            ViewBag.IsStoryList = false;

            return View();
        }

        public async Task<IActionResult> AddChapter(int? storyId)
        {
            var stories = await _context.Stories
                .Include(s => s.StoryCategories)
                .ThenInclude(sc => sc.Category)
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.Title)
                .Select(s => new {
                    s.StoryId, 
                    s.Title,
                    IsTruyenChu = s.StoryCategories.Any(c => c.Category.Name.ToLower().Contains("chữ") || c.Category.Name.ToLower().Contains("novel"))
                })
                .ToListAsync();

            ViewBag.Stories = stories;
            ViewBag.SelectedStoryId = storyId;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddChapter(int storyId, int chapterNumber, string? title, string? content, List<IFormFile>? chapterImages)
        {
            if (storyId <= 0 || (string.IsNullOrWhiteSpace(content) && (chapterImages == null || !chapterImages.Any())))
            {
                ModelState.AddModelError("", "Vui lòng chọn truyện và nhập nội dung chương (hoặc tải ảnh lên).");
                var stories = await _context.Stories
                    .Include(s => s.StoryCategories)
                    .ThenInclude(sc => sc.Category)
                    .Where(s => !s.IsDeleted)
                    .Select(s => new {
                        s.StoryId, 
                        s.Title,
                        IsTruyenChu = s.StoryCategories.Any(c => c.Category.Name.ToLower().Contains("chữ") || c.Category.Name.ToLower().Contains("novel"))
                    }).ToListAsync();
                ViewBag.Stories = stories;
                ViewBag.SelectedStoryId = storyId;
                ViewBag.ChapterNumber = chapterNumber;
                ViewBag.ChapterTitle = title;
                return View();
            }

            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int adminUserId)) return Unauthorized();
            var adminRoleName = User.FindFirstValue(ClaimTypes.Role) ?? "Moderator";

            var finalContent = content ?? "";

            // Xử lý upload ảnh nếu có
            if (chapterImages != null && chapterImages.Any())
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "chapters", storyId.ToString());
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var imageUrls = new List<string>();
                foreach (var image in chapterImages)
                {
                    if (image.Length > 0)
                    {
                        var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(image.FileName);
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await image.CopyToAsync(fileStream);
                        }

                        imageUrls.Add($"/images/chapters/{storyId}/{uniqueFileName}");
                    }
                }

                if (imageUrls.Any())
                {
                    // Nối các URL ảnh bằng dấu xuống dòng hoặc thêm vào content hiện có
                    var imgStr = string.Join("\n", imageUrls);
                    if (string.IsNullOrWhiteSpace(finalContent))
                        finalContent = imgStr;
                    else
                        finalContent += "\n" + imgStr;
                }
            }

            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_Admin_CreateChapter @AdminUserId, @AdminRoleName, @StoryId, @ChapterNumber, @Title, @Content",
                    new SqlParameter("@AdminUserId", adminUserId),
                    new SqlParameter("@AdminRoleName", adminRoleName),
                    new SqlParameter("@StoryId", storyId),
                    new SqlParameter("@ChapterNumber", chapterNumber),
                    new SqlParameter("@Title", title?.Trim() ?? (object)DBNull.Value),
                    new SqlParameter("@Content", finalContent)
                );

                TempData["SuccessMessage"] = "Thêm chương mới thành công!";
                return RedirectToAction("Chapters", new { storyId = storyId });
            }
            catch (SqlException ex)
            {
                ModelState.AddModelError("", "Lỗi CSDL: " + ex.Message);
                var stories = await _context.Stories
                    .Include(s => s.StoryCategories)
                    .ThenInclude(sc => sc.Category)
                    .Where(s => !s.IsDeleted)
                    .Select(s => new {
                        s.StoryId, 
                        s.Title,
                        IsTruyenChu = s.StoryCategories.Any(c => c.Category.Name.ToLower().Contains("chữ") || c.Category.Name.ToLower().Contains("novel"))
                    }).ToListAsync();
                ViewBag.Stories = stories;
                ViewBag.SelectedStoryId = storyId;
                ViewBag.ChapterNumber = chapterNumber;
                ViewBag.ChapterTitle = title;
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> EditChapter(int id)
        {
            var chapter = await _context.Chapters
                .Include(c => c.Story)
                .ThenInclude(s => s.StoryCategories)
                .ThenInclude(sc => sc.Category)
                .FirstOrDefaultAsync(c => c.ChapterId == id && !c.IsDeleted);

            if (chapter == null) return NotFound();

            var stories = await _context.Stories
                .Include(s => s.StoryCategories)
                .ThenInclude(sc => sc.Category)
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.Title)
                .Select(s => new {
                    s.StoryId, 
                    s.Title,
                    IsTruyenChu = s.StoryCategories.Any(c => c.Category.Name.ToLower().Contains("chữ") || c.Category.Name.ToLower().Contains("novel"))
                })
                .ToListAsync();

            ViewBag.Stories = stories;
            return View(chapter);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditChapter(int chapterId, int chapterNumber, string? title, string? content, List<IFormFile>? chapterImages)
        {
            var finalContent = content ?? "";
            
            // Xử lý upload ảnh nếu có
            if (chapterImages != null && chapterImages.Any())
            {
                // Cần lấy storyId từ DB để lưu đúng thư mục nhánh
                var chapter = await _context.Chapters.AsNoTracking().FirstOrDefaultAsync(c => c.ChapterId == chapterId);
                var storyId = chapter?.StoryId ?? 0;

                if (storyId > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "chapters", storyId.ToString());
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    var imageUrls = new List<string>();
                    foreach (var image in chapterImages)
                    {
                        if (image.Length > 0)
                        {
                            var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(image.FileName);
                            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await image.CopyToAsync(fileStream);
                            }

                            imageUrls.Add($"/images/chapters/{storyId}/{uniqueFileName}");
                        }
                    }

                    if (imageUrls.Any())
                    {
                        var imgStr = string.Join("\n", imageUrls);
                        if (string.IsNullOrWhiteSpace(finalContent))
                            finalContent = imgStr;
                        else
                            finalContent += "\n" + imgStr; // Nối sau các ảnh cũ
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(finalContent))
            {
                ModelState.AddModelError("", "Nội dung chương không được để trống (hoặc tải ít nhất 1 ảnh).");
                var stories = await _context.Stories
                    .Include(s => s.StoryCategories)
                    .ThenInclude(sc => sc.Category)
                    .Where(s => !s.IsDeleted)
                    .Select(s => new {
                        s.StoryId, 
                        s.Title,
                        IsTruyenChu = s.StoryCategories.Any(c => c.Category.Name.ToLower().Contains("chữ") || c.Category.Name.ToLower().Contains("novel"))
                    }).ToListAsync();
                ViewBag.Stories = stories;
                return View();
            }

            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int adminUserId)) return Unauthorized();
            var adminRoleName = User.FindFirstValue(ClaimTypes.Role) ?? "Moderator";

            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_Admin_UpdateChapter @AdminUserId, @AdminRoleName, @ChapterId, @ChapterNumber, @Title, @Content",
                    new SqlParameter("@AdminUserId", adminUserId),
                    new SqlParameter("@AdminRoleName", adminRoleName),
                    new SqlParameter("@ChapterId", chapterId),
                    new SqlParameter("@ChapterNumber", chapterNumber),
                    new SqlParameter("@Title", title?.Trim() ?? (object)DBNull.Value),
                    new SqlParameter("@Content", finalContent)
                );

                TempData["SuccessMessage"] = "Cập nhật chương thành công!";
                return RedirectToAction("Chapters");
            }
            catch (SqlException ex)
            {
                ModelState.AddModelError("", "Lỗi CSDL: " + ex.Message);
                var stories = await _context.Stories
                    .Include(s => s.StoryCategories)
                    .ThenInclude(sc => sc.Category)
                    .Where(s => !s.IsDeleted)
                    .Select(s => new {
                        s.StoryId, 
                        s.Title,
                        IsTruyenChu = s.StoryCategories.Any(c => c.Category.Name.ToLower().Contains("chữ") || c.Category.Name.ToLower().Contains("novel"))
                    }).ToListAsync();
                ViewBag.Stories = stories;
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteChapter(int chapterId)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int adminUserId)) return Unauthorized();
            var adminRoleName = User.FindFirstValue(ClaimTypes.Role) ?? "Moderator";

            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_Admin_DeleteChapter @AdminUserId, @AdminRoleName, @ChapterId",
                    new SqlParameter("@AdminUserId", adminUserId),
                    new SqlParameter("@AdminRoleName", adminRoleName),
                    new SqlParameter("@ChapterId", chapterId)
                );
                TempData["SuccessMessage"] = "Đã xóa chương!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Không thể xóa: " + ex.Message;
            }

            return RedirectToAction("Chapters");
        }

        // =====================================================
        // QUẢN LÝ NGƯỜI DÙNG
        // =====================================================

        public async Task<IActionResult> Users(string? search, string? role, int page = 1)
        {
            const int pageSize = 20;
            var query = _context.Users.Where(u => !u.IsDeleted).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u => u.Username.Contains(search) || u.Email.Contains(search));

            if (!string.IsNullOrWhiteSpace(role))
                query = query.Where(u => u.Role.RoleName == role);

            var total = await query.CountAsync();

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    u.UserId, u.Username, u.Email, u.Avatar, u.Status, u.CreatedAt,
                    RoleName = u.Role.RoleName
                })
                .ToListAsync();

            var roles = await _context.Roles.Select(r => r.RoleName).ToListAsync();

            ViewBag.Users = users;
            ViewBag.Roles = roles;
            ViewBag.Search = search;
            ViewBag.RoleFilter = role;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUserStatus(int userId, string status)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int adminUserId)) return Unauthorized();
            var adminRoleName = User.FindFirstValue(ClaimTypes.Role) ?? "Moderator";

            var validStatuses = new[] { "Active", "Banned" };
            if (!validStatuses.Contains(status))
            {
                TempData["ErrorMessage"] = "Trạng thái không hợp lệ.";
                return RedirectToAction("Users");
            }

            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_Admin_UpdateUserStatus @AdminUserId, @AdminRoleName, @TargetUserId, @Status",
                    new SqlParameter("@AdminUserId", adminUserId),
                    new SqlParameter("@AdminRoleName", adminRoleName),
                    new SqlParameter("@TargetUserId", userId),
                    new SqlParameter("@Status", status)
                );
                TempData["SuccessMessage"] = $"Đã cập nhật trạng thái người dùng thành {status}!";
            }
            catch (SqlException ex)
            {
                TempData["ErrorMessage"] = "Lỗi CSDL: " + ex.Message;
            }

            return RedirectToAction("Users");
        }

        // =====================================================
        // QUẢN LÝ PHÂN QUYỀN
        // =====================================================

        public async Task<IActionResult> Roles()
        {
            var roles = await _context.Roles
                .Select(r => new
                {
                    r.RoleId, r.RoleName, r.Description,
                    UserCount = r.Users.Count(u => !u.IsDeleted)
                })
                .ToListAsync();

            ViewBag.Roles = roles;
            return View();
        }

        [HttpGet]
        public IActionResult AddRole()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRole(string roleName, string description)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                TempData["ErrorMessage"] = "Tên quyền không được để trống.";
                return RedirectToAction("Roles");
            }

            // Normal checking (no SP for roles yet, so we do direct EF core manipulations)
            if (await _context.Roles.AnyAsync(r => r.RoleName == roleName))
            {
                TempData["ErrorMessage"] = "Quyền này đã tồn tại.";
                return RedirectToAction("Roles");
            }

            var role = new Models.Entities.Role
            {
                RoleName = roleName.Trim(),
                Description = description?.Trim()
            };

            _context.Roles.Add(role);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Thêm quyền mới thành công!";
            return RedirectToAction("Roles");
        }

        [HttpGet]
        public async Task<IActionResult> EditRole(int id)
        {
            var role = await _context.Roles.FindAsync(id);
            if (role == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy quyền này.";
                return RedirectToAction("Roles");
            }
            return View(role);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRole(int roleId, string roleName, string description)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                TempData["ErrorMessage"] = "Tên quyền không được rỗng.";
                return RedirectToAction("Roles");
            }

            var role = await _context.Roles.FindAsync(roleId);
            if (role == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy quyền này.";
                return RedirectToAction("Roles");
            }

            // Check if another role exists with same name
            if (await _context.Roles.AnyAsync(r => r.RoleName == roleName && r.RoleId != roleId))
            {
                TempData["ErrorMessage"] = "Tên quyền này đã tồn tại.";
                return RedirectToAction("Roles");
            }

            role.RoleName = roleName.Trim();
            role.Description = description?.Trim();

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Cập nhật quyền thành công!";
            
            return RedirectToAction("Roles");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRole(int roleId)
        {
            var role = await _context.Roles.Include(r => r.Users).FirstOrDefaultAsync(r => r.RoleId == roleId);
            if (role == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy quyền này.";
                return RedirectToAction("Roles");
            }

            if (role.RoleName == "Admin" || role.RoleName == "User")
            {
                TempData["ErrorMessage"] = "Không thể xóa quyền hệ thống cơ bản (Admin, User).";
                return RedirectToAction("Roles");
            }

            if (role.Users.Any(u => !u.IsDeleted))
            {
                TempData["ErrorMessage"] = "Không thể xóa quyền đang có người dùng sinh hoạt.";
                return RedirectToAction("Roles");
            }

            _context.Roles.Remove(role);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã xóa quyền thành công!";
            return RedirectToAction("Roles");
        }

        // =====================================================
        // QUẢN LÝ BÌNH LUẬN
        // =====================================================

        public async Task<IActionResult> Comments(string? search, bool showDeleted = false, int page = 1)
        {
            const int pageSize = 25;

            var query = _context.Comments.AsQueryable();

            if (!showDeleted)
                query = query.Where(c => !c.IsDeleted);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c => c.Content.Contains(search) ||
                                         c.User.Username.Contains(search) ||
                                         c.Story.Title.Contains(search));

            var total = await query.CountAsync();

            var comments = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    c.CommentId, c.Content, c.CreatedAt, c.IsDeleted,
                    Username = c.User.Username,
                    StoryTitle = c.Story.Title,
                    StorySlug = c.Story.Slug
                })
                .ToListAsync();

            ViewBag.Comments = comments;
            ViewBag.Search = search;
            ViewBag.ShowDeleted = showDeleted;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);

            return View();
        }

        // =====================================================
        // QUẢN LÝ ĐÁNH GIÁ
        // =====================================================

        public async Task<IActionResult> Ratings(int? storyId, int? minRating, int page = 1)
        {
            const int pageSize = 25;
            var query = _context.Ratings.AsQueryable();

            if (storyId.HasValue)
                query = query.Where(r => r.StoryId == storyId.Value);

            if (minRating.HasValue)
                query = query.Where(r => r.RatingValue >= minRating.Value);

            var total = await query.CountAsync();

            var ratings = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    r.RatingId, r.RatingValue, r.CreatedAt,
                    Username = r.User.Username,
                    StoryTitle = r.Story.Title,
                    r.StoryId
                })
                .ToListAsync();

            var stories = await _context.Stories
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.Title)
                .Select(s => new { s.StoryId, s.Title, s.RatingAvg })
                .ToListAsync();

            ViewBag.Ratings = ratings;
            ViewBag.Stories = stories;
            ViewBag.FilterStoryId = storyId;
            ViewBag.MinRating = minRating;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);

            return View();
        }

        // =====================================================
        // QUẢN LÝ THỂ LOẠI
        // =====================================================

        public async Task<IActionResult> Categories(string? search)
        {
            var query = _context.Categories.Where(c => !c.IsDeleted).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c => c.Name.Contains(search));

            var categories = await query
                .OrderBy(c => c.Name)
                .Select(c => new
                {
                    c.CategoryId, c.Name, c.Slug, c.Description, c.CreatedAt,
                    MangaCount = c.StoryCategories.Count(sc => !sc.Story.IsDeleted)
                })
                .ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.Search = search;
            return View();
        }

        [HttpGet]
        public IActionResult AddCategory()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCategory(string name, string description)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ErrorMessage"] = "Tên thể loại không được rỗng.";
                return RedirectToAction("Categories");
            }

            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int adminUserId)) return Unauthorized();
            var adminRoleName = User.FindFirstValue(ClaimTypes.Role) ?? "Moderator";
            var slug = GenerateSlug(name);

            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_Admin_CreateCategory @AdminUserId, @AdminRoleName, @Name, @Slug, @Description",
                    new SqlParameter("@AdminUserId", adminUserId),
                    new SqlParameter("@AdminRoleName", adminRoleName),
                    new SqlParameter("@Name", name.Trim()),
                    new SqlParameter("@Slug", slug),
                    new SqlParameter("@Description", description?.Trim() ?? (object)DBNull.Value)
                );
                TempData["SuccessMessage"] = "Thêm thể loại thành công!";
            }
            catch (SqlException ex)
            {
                TempData["ErrorMessage"] = "Lỗi CSDL: " + ex.Message;
            }

            return RedirectToAction("Categories");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(int categoryId, string name, string description)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ErrorMessage"] = "Tên thể loại không được rỗng.";
                return RedirectToAction("Categories");
            }

            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int adminUserId)) return Unauthorized();
            var adminRoleName = User.FindFirstValue(ClaimTypes.Role) ?? "Moderator";
            var slug = GenerateSlug(name);

            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_Admin_UpdateCategory @AdminUserId, @AdminRoleName, @CategoryId, @Name, @Slug, @Description",
                    new SqlParameter("@AdminUserId", adminUserId),
                    new SqlParameter("@AdminRoleName", adminRoleName),
                    new SqlParameter("@CategoryId", categoryId),
                    new SqlParameter("@Name", name.Trim()),
                    new SqlParameter("@Slug", slug),
                    new SqlParameter("@Description", description?.Trim() ?? (object)DBNull.Value)
                );
                TempData["SuccessMessage"] = "Cập nhật thể loại thành công!";
            }
            catch (SqlException ex)
            {
                TempData["ErrorMessage"] = "Lỗi CSDL: " + ex.Message;
            }

            return RedirectToAction("Categories");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int categoryId)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int adminUserId)) return Unauthorized();
            var adminRoleName = User.FindFirstValue(ClaimTypes.Role) ?? "Moderator";

            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "EXEC sp_Admin_DeleteCategory @AdminUserId, @AdminRoleName, @CategoryId",
                    new SqlParameter("@AdminUserId", adminUserId),
                    new SqlParameter("@AdminRoleName", adminRoleName),
                    new SqlParameter("@CategoryId", categoryId)
                );
                TempData["SuccessMessage"] = "Đã xóa thể loại!";
            }
            catch (SqlException ex)
            {
                TempData["ErrorMessage"] = "Không thể xóa: " + ex.Message;
            }

            return RedirectToAction("Categories");
        }

        // =====================================================
        // THỐNG KÊ / ANALYTICS
        // =====================================================

        public async Task<IActionResult> Analytics()
        {
            // Tổng quát
            ViewBag.TotalStories = await _context.Stories.CountAsync(s => !s.IsDeleted);
            ViewBag.TotalUsers = await _context.Users.CountAsync(u => !u.IsDeleted);
            ViewBag.TotalComments = await _context.Comments.CountAsync(c => !c.IsDeleted);
            ViewBag.TotalRatings = await _context.Ratings.CountAsync();

            // Top 10 truyện được xem nhiều nhất
            var topViewedStories = await _context.Stories
                .Where(s => !s.IsDeleted)
                .OrderByDescending(s => s.ViewCount)
                .Take(10)
                .Select(s => new { s.Title, s.ViewCount, s.RatingAvg })
                .ToListAsync();

            // Phân bổ thể loại (Top 10 thể loại nhiều truyện nhất)
            var categoryDistribution = await _context.Categories
                .Where(c => !c.IsDeleted)
                .Select(c => new
                {
                    c.Name,
                    Count = c.StoryCategories.Count(sc => !sc.Story.IsDeleted)
                })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToListAsync();

            // Users mới trong 30 ngày gần đây (theo ngày)
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var newUsersPerDay = await _context.Users
                .Where(u => u.CreatedAt >= thirtyDaysAgo)
                .GroupBy(u => u.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToListAsync();

            ViewBag.TopViewedStories = topViewedStories;
            ViewBag.CategoryDistribution = categoryDistribution;
            ViewBag.NewUsersPerDay = newUsersPerDay;

            return View();
        }
    }
}
