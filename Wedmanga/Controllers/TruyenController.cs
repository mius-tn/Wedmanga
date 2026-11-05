using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wedmanga.Data;
using Wedmanga.Models.Entities;

namespace Wedmanga.Controllers
{
    public class TruyenController : Controller
    {
        private readonly MangaDbContext _context;

        public TruyenController(MangaDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Chi tiết truyện theo slug.
        /// Route: /truyen/{slug}
        /// </summary>
        public async Task<IActionResult> Details(string slug)
        {
            if (string.IsNullOrEmpty(slug)) return NotFound();

            var story = await _context.Stories
                .Include(s => s.Chapters.Where(c => !c.IsDeleted).OrderByDescending(c => c.ChapterNumber))
                .Include(s => s.StoryCategories).ThenInclude(sc => sc.Category)
                .Include(s => s.Comments.Where(c => !c.IsDeleted).OrderByDescending(c => c.CreatedAt).Take(20)).ThenInclude(c => c.User)
                .FirstOrDefaultAsync(s => s.Slug == slug && !s.IsDeleted);

            if (story == null) return NotFound();

            // Tăng view count
            var storyEntity = await _context.Stories.FindAsync(story.StoryId);
            if (storyEntity != null)
            {
                storyEntity.ViewCount++;
                await _context.SaveChangesAsync();
            }

            ViewBag.Story = story;
            return View();
        }

        /// <summary>
        /// Danh sách truyện theo kiểu (Manga hoặc Light Novel), có phân trang.
        /// Route: /the-loai-truyen/{type}?page=1
        /// </summary>
        public async Task<IActionResult> Type(string type, string category = "all", string status = "all", string sort = "views", int page = 1)
        {
            if (string.IsNullOrEmpty(type)) return NotFound();

            // Lấy danh sách thể loại để lọc
            ViewBag.AllCategories = await _context.Categories.Where(c => !c.IsDeleted).ToListAsync();
            ViewBag.CurrentCategory = category;

            // Chuyển đổi slug thành tên thật trong DB
            string dbType = type == "Light-Novel" ? "Light Novel" : type;

            const int pageSize = 24;

            var query = _context.Stories.Where(s => s.Type == dbType && !s.IsDeleted);

            // Filter by Category
            if (!string.IsNullOrEmpty(category) && category != "all")
            {
                query = query.Where(s => s.StoryCategories.Any(sc => sc.Category.Slug == category));
            }

            // Filter by Status
            if (status == "ongoing")
            {
                query = query.Where(s => s.Status == "Đang tiến hành" || s.Status == "Ongoing");
            }
            else if (status == "completed")
            {
                query = query.Where(s => s.Status == "Hoàn thành" || s.Status == "Completed");
            }

            // Sort
            switch (sort)
            {
                case "newest":
                    query = query.OrderByDescending(s => s.CreatedAt);
                    break;
                case "rating":
                    query = query.OrderByDescending(s => s.RatingAvg);
                    break;
                case "az":
                    query = query.OrderBy(s => s.Title);
                    break;
                case "views":
                default:
                    query = query.OrderByDescending(s => s.ViewCount);
                    break;
            }

            var total = await query.CountAsync();

            var stories = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Type = type;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentSort = sort;
            ViewBag.Stories = stories;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);

            return View();
        }

        /// <summary>
        /// Danh sách truyện theo thể loại, có phân trang.
        /// Route: /the-loai/{slug}?page=1
        /// </summary>
        public async Task<IActionResult> Category(string slug, string status = "all", string sort = "newest", int page = 1)
        {
            if (string.IsNullOrEmpty(slug)) slug = "tat-ca";

            const int pageSize = 24;

            // Fetch all categories for the filter
            ViewBag.AllCategories = await _context.Categories.Where(c => !c.IsDeleted).ToListAsync();
            ViewBag.CurrentSlug = slug;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentSort = sort;

            IQueryable<Story> query = _context.Stories.Where(s => !s.IsDeleted);

            Wedmanga.Models.Entities.Category category = null;
            if (slug != "tat-ca")
            {
                category = await _context.Categories
                    .FirstOrDefaultAsync(c => c.Slug == slug && !c.IsDeleted);

                if (category == null) return NotFound();

                query = query.Where(s => s.StoryCategories.Any(sc => sc.CategoryId == category.CategoryId));
            }

            ViewBag.Category = category;

            // Filter by Status
            if (status == "ongoing")
            {
                query = query.Where(s => s.Status == "Đang tiến hành" || s.Status == "Ongoing");
            }
            else if (status == "completed")
            {
                query = query.Where(s => s.Status == "Hoàn thành" || s.Status == "Completed");
            }

            // Sort
            switch (sort)
            {
                case "views":
                    query = query.OrderByDescending(s => s.ViewCount);
                    break;
                case "rating":
                    query = query.OrderByDescending(s => s.RatingAvg);
                    break;
                case "az":
                    query = query.OrderBy(s => s.Title);
                    break;
                case "newest":
                default:
                    query = query.OrderByDescending(s => s.CreatedAt);
                    break;
            }

            var total = await query.CountAsync();

            var stories = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Stories = stories;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);

            return View();
        }

        /// <summary>
        /// Đọc chương truyện.
        /// Route: /truyen/{storySlug}/chuong/{chapterNumber}
        /// </summary>
        public async Task<IActionResult> Read(string storySlug, int chapterNumber)
        {
            if (string.IsNullOrEmpty(storySlug)) return NotFound();

            var chapter = await _context.Chapters
                .Include(c => c.Story).ThenInclude(s => s.Chapters.Where(ch => !ch.IsDeleted).OrderByDescending(ch => ch.ChapterNumber))
                .FirstOrDefaultAsync(c => c.Story.Slug == storySlug && c.ChapterNumber == chapterNumber && !c.IsDeleted);

            if (chapter == null) return NotFound();

            ViewBag.PrevChapter = await _context.Chapters
                .Where(x => x.StoryId == chapter.StoryId && x.ChapterNumber < chapterNumber && !x.IsDeleted)
                .OrderByDescending(x => x.ChapterNumber)
                .Select(x => x.ChapterNumber)
                .FirstOrDefaultAsync();

            ViewBag.NextChapter = await _context.Chapters
                .Where(x => x.StoryId == chapter.StoryId && x.ChapterNumber > chapterNumber && !x.IsDeleted)
                .OrderBy(x => x.ChapterNumber)
                .Select(x => x.ChapterNumber)
                .FirstOrDefaultAsync();

            // Tăng view count của chương
            var chapterEntity = await _context.Chapters.FindAsync(chapter.ChapterId);
            if (chapterEntity != null)
            {
                chapterEntity.ViewCount++;
                await _context.SaveChangesAsync();
            }

            ViewBag.Chapter = chapter;
            return View();
        }
    }
}
