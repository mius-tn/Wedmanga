using System;
using System.ComponentModel.DataAnnotations;

namespace Wedmanga.Models.Entities
{
    public class Bookmark
    {
        [Key]
        public int BookmarkId { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public int StoryId { get; set; }
        public Story Story { get; set; } = null!;
        public int? ChapterId { get; set; }
        public Chapter? Chapter { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ReadingHistory
    {
        [Key]
        public int HistoryId { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public int StoryId { get; set; }
        public Story Story { get; set; } = null!;
        public int ChapterId { get; set; }
        public Chapter Chapter { get; set; } = null!;
        public int LastPosition { get; set; } = 0;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
