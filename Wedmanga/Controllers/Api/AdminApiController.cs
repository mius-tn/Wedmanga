using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedmanga.Data;
using Wedmanga.Models.Entities;

namespace Wedmanga.Controllers.Api
{
    [Route("api/quan-tri")]
    [ApiController]
    [Authorize(Roles = "Admin,Moderator")]
    public class AdminApiController : ControllerBase
    {
        private readonly MangaDbContext _context;

        public AdminApiController(MangaDbContext context)
        {
            _context = context;
        }

        public class StoryRequest
        {
            public string Title { get; set; } = null!;
            public string Slug { get; set; } = null!;
            public string? Description { get; set; }
            public string? Author { get; set; }
            public string? CoverImage { get; set; }
        }

        // POST /api/quan-tri/truyen
        [HttpPost("truyen")]
        public async Task<IActionResult> CreateStory([FromBody] StoryRequest req)
        {
            var story = new Story
            {
                Title = req.Title,
                Slug = req.Slug,
                Description = req.Description,
                Author = req.Author,
                CoverImage = req.CoverImage
            };
            _context.Stories.Add(story);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Tạo truyện thành công", storyId = story.StoryId });
        }

        // PUT /api/quan-tri/truyen/{id}
        [HttpPut("truyen/{id}")]
        public async Task<IActionResult> UpdateStory(int id, [FromBody] StoryRequest req)
        {
            var story = await _context.Stories.FindAsync(id);
            if (story == null) return NotFound();

            story.Title = req.Title;
            story.Slug = req.Slug;
            story.Description = req.Description;
            story.Author = req.Author;
            story.CoverImage = req.CoverImage;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật thành công." });
        }

        // DELETE /api/quan-tri/truyen/{id}
        [HttpDelete("truyen/{id}")]
        public async Task<IActionResult> DeleteStory(int id)
        {
            var story = await _context.Stories.FindAsync(id);
            if (story == null) return NotFound();

            story.IsDeleted = true; // Soft Delete
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã xóa truyện (Soft Delete)." });
        }

        public class ChapterRequest
        {
            public int StoryId { get; set; }
            public int ChapterNumber { get; set; }
            public string Title { get; set; } = null!;
            public string Content { get; set; } = null!;
        }

        // POST /api/quan-tri/chuong
        [HttpPost("chuong")]
        public async Task<IActionResult> AddChapter([FromBody] ChapterRequest req)
        {
            var chapter = new Chapter
            {
                StoryId = req.StoryId,
                ChapterNumber = req.ChapterNumber,
                Title = req.Title,
                Content = req.Content
            };
            _context.Chapters.Add(chapter);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Thêm chương mới thành công", chapterId = chapter.ChapterId });
        }
    }
}
