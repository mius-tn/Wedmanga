using Microsoft.EntityFrameworkCore;
using Wedmanga.Data;
using Wedmanga.Models.DTOs;
using Wedmanga.Models.Entities;
using Wedmanga.Repositories.Interfaces;

namespace Wedmanga.Repositories
{
    /// <summary>
    /// Triển khai ICategoryRepository.
    /// Tầng này CHỈ giao tiếp với Database qua EF Core.
    /// Không chứa Business Logic.
    /// </summary>
    public class CategoryRepository : ICategoryRepository
    {
        private readonly MangaDbContext _context;

        public CategoryRepository(MangaDbContext context)
        {
            _context = context;
        }

        // ---- READ OPERATIONS ----

        public async Task<IEnumerable<CategorySummaryDto>> GetAllAsync()
        {
            return await _context.Categories
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Name)
                .Select(c => new CategorySummaryDto
                {
                    CategoryId = c.CategoryId,
                    Name = c.Name,
                    Slug = c.Slug,
                    Description = c.Description,
                    // Đếm số truyện đang hoạt động trong thể loại này
                    MangaCount = c.StoryCategories.Count(sc => !sc.Story.IsDeleted)
                })
                .ToListAsync();
        }

        public async Task<CategoryDetailDto?> GetBySlugAsync(string slug, int page, int pageSize)
        {
            // Lấy category cơ bản
            var category = await _context.Categories
                .Where(c => c.Slug == slug && !c.IsDeleted)
                .FirstOrDefaultAsync();

            if (category == null) return null;

            // Đếm tổng số truyện để tính Pagination
            var totalMangas = await _context.StoryCategories
                .Where(sc => sc.CategoryId == category.CategoryId && !sc.Story.IsDeleted)
                .CountAsync();

            var totalPages = (int)Math.Ceiling((double)totalMangas / pageSize);

            // Lấy truyện phân trang - sắp xếp theo lượt xem giảm dần
            var mangas = await _context.StoryCategories
                .Where(sc => sc.CategoryId == category.CategoryId && !sc.Story.IsDeleted)
                .Select(sc => new MangaSummaryDto
                {
                    StoryId = sc.Story.StoryId,
                    Title = sc.Story.Title,
                    Slug = sc.Story.Slug,
                    CoverImage = sc.Story.CoverImage,
                    RatingAvg = sc.Story.RatingAvg,
                    ViewCount = sc.Story.ViewCount,
                    Status = sc.Story.Status
                })
                .OrderByDescending(m => m.ViewCount)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new CategoryDetailDto
            {
                CategoryId = category.CategoryId,
                Name = category.Name,
                Slug = category.Slug,
                Description = category.Description,
                TotalMangas = totalMangas,
                CurrentPage = page,
                TotalPages = totalPages,
                Mangas = mangas
            };
        }

        public async Task<Category?> FindByIdAsync(int id)
        {
            return await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryId == id && !c.IsDeleted);
        }

        public async Task<bool> IsSlugExistsAsync(string slug)
        {
            return await _context.Categories.AnyAsync(c => c.Slug == slug && !c.IsDeleted);
        }

        public async Task<bool> AreAllCategoryIdsValidAsync(IEnumerable<int> ids)
        {
            var idList = ids.Distinct().ToList();
            if (!idList.Any()) return true;

            var validCount = await _context.Categories
                .CountAsync(c => idList.Contains(c.CategoryId) && !c.IsDeleted);

            return validCount == idList.Count;
        }

        // ---- WRITE OPERATIONS ----

        public Task<Category> AddAsync(Category category)
        {
            _context.Categories.Add(category);
            return Task.FromResult(category);
        }

        public Task UpdateAsync(Category category)
        {
            _context.Categories.Update(category);
            return Task.CompletedTask;
        }

        public async Task<bool> SoftDeleteAsync(int id)
        {
            var category = await FindByIdAsync(id);
            if (category == null) return false;

            category.IsDeleted = true;
            category.UpdatedAt = DateTime.UtcNow;
            _context.Categories.Update(category);
            return true;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
