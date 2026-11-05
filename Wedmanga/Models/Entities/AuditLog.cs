using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Wedmanga.Models.Entities
{
    public class AuditLog
    {
        [Key]
        public int LogId { get; set; }

        public int? AdminUserId { get; set; }

        [MaxLength(50)]
        public string ActionType { get; set; } = null!; // CREATE, UPDATE, DELETE, RESTORE

        [MaxLength(100)]
        public string TableName { get; set; } = null!;

        [MaxLength(100)]
        public string RecordId { get; set; } = null!;

        public string? OldValues { get; set; } // JSON
        public string? NewValues { get; set; } // JSON

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("AdminUserId")]
        public User? AdminUser { get; set; }
    }
}
