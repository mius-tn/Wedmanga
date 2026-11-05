using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wedmanga.Data;
using Wedmanga.Models;
using Wedmanga.Models.Entities;

namespace Wedmanga.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly MangaDbContext _context;

        public HomeController(ILogger<HomeController> logger, MangaDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        /// <summary>Trang chủ - Hiển thị truyện nổi bật, mới cập nhật, top xem</summary>
        public async Task<IActionResult> Index(int page = 1)
        {
            // Hot/trending: top 8 lượt xem
            var hotStories = await _context.Stories
                .Where(s => !s.IsDeleted)
                .OrderByDescending(s => s.ViewCount)
                .Take(8)
                .ToListAsync();

            // Mới nhất: 12 truyện tạo gần đây nhất
            var newStories = await _context.Stories
                .Where(s => !s.IsDeleted)
                .OrderByDescending(s => s.CreatedAt)
                .Take(12)
                .ToListAsync();

            // Thể loại để hiển thị trên nav/filter
            var categories = await _context.Categories
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Name)
                .Select(c => new { c.CategoryId, c.Name, c.Slug })
                .ToListAsync();

            // Paged Stories for the new Grid Section
            int pageSize = 10;
            int offset = (page - 1) * pageSize;
            var query = _context.Stories
                .Include(s => s.Chapters.Where(c => !c.IsDeleted).OrderByDescending(c => c.ChapterNumber).Take(1))
                .Where(s => !s.IsDeleted)
                .OrderByDescending(s => s.CreatedAt);
            
            var totalPaged = await query.CountAsync();
            var pagedStories = await query.Skip(offset).Take(pageSize).ToListAsync();

            ViewBag.HotStories = hotStories;
            ViewBag.NewStories = newStories;
            ViewBag.Categories = categories;
            ViewBag.PagedStories = pagedStories;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalPaged / pageSize);
            ViewBag.TrendingTop5 = hotStories.Take(5).ToList();

            return View();
        }

        /// <summary>Bảng Xếp Hạng - Top truyện theo lượt xem/rating</summary>
        public async Task<IActionResult> Ranking(string type = "view", int page = 1)
        {
            const int pageSize = 20;
            var offset = (page - 1) * pageSize;

            IQueryable<Story> query = _context.Stories.Where(s => !s.IsDeleted);

            query = type switch
            {
                "rating" => query.OrderByDescending(s => s.RatingAvg),
                _ => query.OrderByDescending(s => s.ViewCount)
            };

            var total = await query.CountAsync();
            var stories = await query
                .Skip(offset).Take(pageSize)
                .ToListAsync();

            ViewBag.Stories = stories;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
            ViewBag.Type = type;

            return View();
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
