using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wedmanga.Data;
using Wedmanga.Models.Entities;

namespace Wedmanga.Controllers.Api
{
    [ApiController]
    public class TruyenApiController : ControllerBase
    {
        private readonly MangaDbContext _context;

        public TruyenApiController(MangaDbContext context)
        {
            _context = context;
        }

        // GET /api/truyen (Pagination)
        [HttpGet("api/truyen")]
        public async Task<IActionResult> GetStories(int page = 1, int size = 20)
        {
            var offset = (page - 1) * size;
            var stories = await _context.Stories
                .Where(s => !s.IsDeleted && s.Status != "Dropped")
                .OrderByDescending(s => s.ViewCount)
                .Skip(offset)
                .Take(size)
                .Select(s => new { s.StoryId, s.Title, s.Slug, s.CoverImage, s.RatingAvg, s.ViewCount })
                .ToListAsync();

            return Ok(stories);
        }

        // GET /api/truyen/{slug}
        [HttpGet("api/truyen/{slug}")]
        public async Task<IActionResult> GetStoryDetail(string slug)
        {
            var story = await _context.Stories
                .Where(s => s.Slug == slug && !s.IsDeleted)
                .Select(s => new {
                    s.StoryId, s.Title, s.Description, s.Author, s.CoverImage, s.Status,
                    s.ViewCount, s.RatingAvg,
                    Chapters = s.Chapters.Where(c => !c.IsDeleted).Select(c => new { c.ChapterNumber, c.Title }).OrderByDescending(c => c.ChapterNumber)
                })
                .FirstOrDefaultAsync();

            if (story == null) return NotFound();
            return Ok(story);
        }

        // POST /api/binh-luan
        [HttpPost("api/binh-luan")]
        [Authorize]
        public async Task<IActionResult> PostComment([FromBody] CommentRequest req)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            var comment = new Comment
            {
                UserId = userId,
                StoryId = req.StoryId,
                ChapterId = req.ChapterId,
                Content = req.Content
            };
            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã gửi bình luận." });
        }

        // POST /api/danh-gia
        [HttpPost("api/danh-gia")]
        [Authorize]
        public async Task<IActionResult> PostRating([FromBody] RatingRequest req)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");

            var existing = await _context.Ratings.FirstOrDefaultAsync(r => r.UserId == userId && r.StoryId == req.StoryId);
            if (existing != null)
            {
                existing.RatingValue = req.RatingValue;
            }
            else
            {
                _context.Ratings.Add(new Rating { UserId = userId, StoryId = req.StoryId, RatingValue = req.RatingValue });
            }
            await _context.SaveChangesAsync(); // SQL Server TRG_UpdateRatingAvg will run automatically
            return Ok(new { message = "Đánh giá thành công." });
        }

        // GET /api/bang-xep-hang/tuan
        [HttpGet("api/bang-xep-hang/tuan")]
        public async Task<IActionResult> GetRanking()
        {
            // Tricking Stored Procedure execution
            var ranking = await _context.Database.SqlQueryRaw<RankingDto>("EXEC sp_GetTopWeekRanking").ToListAsync();
            return Ok(ranking);
        }

        public class CommentRequest { public int StoryId { get; set; } public int? ChapterId { get; set; } public string Content { get; set; } = null!; }
        public class RatingRequest { public int StoryId { get; set; } public int RatingValue { get; set; } }
        public class RankingDto { public int StoryId { get; set; } public string Title { get; set; } = null!; public string Slug { get; set; } = null!; public string? CoverImage { get; set; } public int ViewCount { get; set; } public double RatingAvg { get; set; } }
    }
}
