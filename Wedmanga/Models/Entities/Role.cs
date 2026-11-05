using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Wedmanga.Models.Entities
{
    public class Role
    {
        public int RoleId { get; set; }
        [Required]
        [MaxLength(50)]
        public string RoleName { get; set; } = null!;
        [MaxLength(255)]
        public string? Description { get; set; }

        public ICollection<User> Users { get; set; } = new List<User>();
    }
}
