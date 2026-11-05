using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Wedmanga.Models.Entities
{
    public class Chapter
    {
        public int ChapterId { get; set; }
        public int StoryId { get; set; }
        public Story Story { get; set; } = null!;
        public int ChapterNumber { get; set; }
        [MaxLength(255)]
        public string? Title { get; set; }
        public string? Content { get; set; }
        public int ViewCount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }

        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public ICollection<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
        public ICollection<ReadingHistory> ReadingHistories { get; set; } = new List<ReadingHistory>();
    }

    public class Category
    {
        public int CategoryId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = null!;
        
        [Required]
        [MaxLength(100)]
        public string Slug { get; set; } = null!;

        [MaxLength(500)]
        public string? Description { get; set; }

        // Soft Delete - Không xóa vĩnh viễn khỏi DB
        public bool IsDeleted { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation (Many-to-Many với Story)
        public ICollection<StoryCategory> StoryCategories { get; set; } = new List<StoryCategory>();
    }

    public class StoryCategory
    {
        public int StoryId { get; set; }
        public Story Story { get; set; } = null!;
        public int CategoryId { get; set; }
        public Category Category { get; set; } = null!;
    }
}
