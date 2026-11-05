using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Wedmanga.Data;
using Wedmanga.Models.Entities;


namespace Wedmanga.Data
{
    public static class DbInitializer
    {
        public static void Initialize(MangaDbContext context)
        {
            // Use Migrate instead of EnsureCreated to ensure migrations history is tracked
            context.Database.Migrate();

            // Look for any roles.
            if (context.Roles.Any())
            {
                return;   // DB has been seeded
            }

            var roles = new Role[]
            {
                new Role { RoleName = "Admin", Description = "Quản trị viên toàn hệ thống" },
                new Role { RoleName = "Moderator", Description = "Người kiểm duyệt nội dung" },
                new Role { RoleName = "User", Description = "Độc giả, thành viên" }
            };

            context.Roles.AddRange(roles);
            context.SaveChanges();

            var adminRole = context.Roles.FirstOrDefault(r => r.RoleName == "Admin");

            var adminUser = new User
            {
                Username = "admin",
                Email = "admin@mangayom.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"), // Changed password to strong Admin@123
                Avatar = "https://ui-avatars.com/api/?name=Admin&background=8b3dff&color=fff",
                RoleId = adminRole!.RoleId,
                Role = adminRole,
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(adminUser);
            context.SaveChanges();
            
            // Seed a few demo categories
            var categories = new Category[]
            {
                new Category { Name = "Hành Động", Slug = "hanh-dong", Description = "Thể loại bay nhảy, đánh đấm", CreatedAt = DateTime.UtcNow },
                new Category { Name = "Lãng Mạn", Slug = "lang-man", Description = "Tình yêu nam nữ", CreatedAt = DateTime.UtcNow },
                new Category { Name = "Kinh Dị", Slug = "kinh-di", Description = "Ma quỷ, sát nhân", CreatedAt = DateTime.UtcNow }
            };
            
            context.Categories.AddRange(categories);
            context.SaveChanges();
        }
    }
}
