using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Wedmanga.Models.Entities
{
    public class User
    {
        public int UserId { get; set; }
        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = null!;
        [Required]
        [MaxLength(100)]
        public string Email { get; set; } = null!;
        [Required]
        [MaxLength(255)]
        public string PasswordHash { get; set; } = null!;
        [MaxLength(255)]
        public string? Avatar { get; set; }
        
        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;

        [MaxLength(20)]
        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }

        public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public ICollection<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
        public ICollection<ReadingHistory> ReadingHistories { get; set; } = new List<ReadingHistory>();
    }
}
