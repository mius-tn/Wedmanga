using System;
using System.ComponentModel.DataAnnotations;

namespace Wedmanga.Models.Entities
{
    public class Rating
    {
        public int RatingId { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public int StoryId { get; set; }
        public Story Story { get; set; } = null!;
        [Range(1, 5)]
        public int RatingValue { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Comment
    {
        public int CommentId { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public int StoryId { get; set; }
        public Story Story { get; set; } = null!;
        public int? ChapterId { get; set; }
        public Chapter? Chapter { get; set; }
        [Required]
        public string Content { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
    }
}
