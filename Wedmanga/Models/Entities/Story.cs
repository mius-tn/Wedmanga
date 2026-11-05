using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Wedmanga.Models.Entities
{
    public class Story
    {
        public int StoryId { get; set; }
        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = null!;
        [Required]
        [MaxLength(255)]
        public string Slug { get; set; } = null!;
        public string? Description { get; set; }
        [MaxLength(100)]
        public string? Author { get; set; }
        [MaxLength(255)]
        public string? CoverImage { get; set; }
        [MaxLength(20)]
        public string Status { get; set; } = "Ongoing";
        [MaxLength(50)]
        public string Type { get; set; } = "Manga";
        public int ViewCount { get; set; }
        public double RatingAvg { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }

        public ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
        public ICollection<StoryCategory> StoryCategories { get; set; } = new List<StoryCategory>();
        public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public ICollection<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
        public ICollection<ReadingHistory> ReadingHistories { get; set; } = new List<ReadingHistory>();
    }
}
