using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Wedmanga.Data;
using Wedmanga.Models.DTOs;
using Wedmanga.Models.Entities;
using Wedmanga.Repositories.Interfaces;
using Wedmanga.Services.Interfaces;

namespace Wedmanga.Services
{
    /// <summary>
    /// Triển khai ICategoryService.
    /// Tầng này chứa toàn bộ Business Logic, Validation nghiệp vụ.
    /// Giao tiếp với DB qua ICategoryRepository, không dùng DbContext trực tiếp
    /// (ngoại trừ khi cần Transaction phức tạp multi-repo).
    /// </summary>
    public class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _categoryRepo;
        private readonly MangaDbContext _context; // cần cho Transaction phức tạp

        public CategoryService(ICategoryRepository categoryRepo, MangaDbContext context)
        {
            _categoryRepo = categoryRepo;
            _context = context;
        }

        // ================================================================
        // PUBLIC API
        // ================================================================

        public async Task<ApiResponse<IEnumerable<CategorySummaryDto>>> GetAllCategoriesAsync()
        {
            var categories = await _categoryRepo.GetAllAsync();
            return ApiResponse<IEnumerable<CategorySummaryDto>>.Success(categories);
        }

        public async Task<ApiResponse<CategoryDetailDto>> GetMangasByCategorySlugAsync(
            string slug, int page = 1, int pageSize = 20)
        {
            // Validate pagination params
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var result = await _categoryRepo.GetBySlugAsync(slug, page, pageSize);

            if (result == null)
                return ApiResponse<CategoryDetailDto>.Fail($"Thể loại '{slug}' không tồn tại.");

            return ApiResponse<CategoryDetailDto>.Success(result);
        }

        // ================================================================
        // ADMIN API
        // ================================================================

        public async Task<ApiResponse<CategorySummaryDto>> CreateCategoryAsync(CreateCategoryRequest request)
        {
            // 1. Business Validation: Name không trống (đã có DataAnnotation kiểm tra cơ bản)
            if (string.IsNullOrWhiteSpace(request.Name))
                return ApiResponse<CategorySummaryDto>.Fail("Tên thể loại không được để trống.");

            // 2. Auto-generate Slug từ Name (VD: "Hành Động" -> "hanh-dong")
            var slug = GenerateSlug(request.Name);

            // 3. Validate Slug unique
            if (await _categoryRepo.IsSlugExistsAsync(slug))
                return ApiResponse<CategorySummaryDto>.Fail($"Thể loại '{request.Name}' đã tồn tại trong hệ thống.");

            // 4. Tạo Entity và lưu DB
            var category = new Category
            {
                Name = request.Name.Trim(),
                Slug = slug,
                Description = request.Description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _categoryRepo.AddAsync(category);
            await _categoryRepo.SaveChangesAsync();

            // 5. Trả về DTO
            var dto = new CategorySummaryDto
            {
                CategoryId = category.CategoryId,
                Name = category.Name,
                Slug = category.Slug,
                Description = category.Description,
                MangaCount = 0
            };

            return ApiResponse<CategorySummaryDto>.Success(dto, "Tạo thể loại thành công.");
        }

        public async Task<ApiResponse<CategorySummaryDto>> UpdateCategoryAsync(int id, UpdateCategoryRequest request)
        {
            // 1. Tìm Category
            var category = await _categoryRepo.FindByIdAsync(id);
            if (category == null)
                return ApiResponse<CategorySummaryDto>.Fail("Không tìm thấy thể loại.");

            // 2. Cập nhật Name và tái tạo Slug nếu Name thay đổi
            if (!string.IsNullOrWhiteSpace(request.Name) && request.Name != category.Name)
            {
                var newSlug = GenerateSlug(request.Name);

                // Kiểm tra slug mới có trùng với thể loại khác không
                var existingWithSlug = await _context.Categories
                    .AnyAsync(c => c.Slug == newSlug && c.CategoryId != id && !c.IsDeleted);

                if (existingWithSlug)
                    return ApiResponse<CategorySummaryDto>.Fail($"Tên '{request.Name}' đã được sử dụng bởi thể loại khác.");

                category.Name = request.Name.Trim();
                category.Slug = newSlug;
            }

            if (request.Description != null)
                category.Description = request.Description;

            category.UpdatedAt = DateTime.UtcNow;

            await _categoryRepo.UpdateAsync(category);
            await _categoryRepo.SaveChangesAsync();

            return ApiResponse<CategorySummaryDto>.Success(new CategorySummaryDto
            {
                CategoryId = category.CategoryId,
                Name = category.Name,
                Slug = category.Slug,
                Description = category.Description
            }, "Cập nhật thành công.");
        }

        public async Task<ApiResponse<bool>> DeleteCategoryAsync(int id)
        {
            var category = await _categoryRepo.FindByIdAsync(id);
            if (category == null)
                return ApiResponse<bool>.Fail("Không tìm thấy thể loại.");

            // Kiểm tra xem Category có đang được dùng bởi Truyện nào không
            var mangaCount = await _context.StoryCategories
                .CountAsync(sc => sc.CategoryId == id && !sc.Story.IsDeleted);

            if (mangaCount > 0)
            {
                // Best Practice: Không cho xóa nếu đang được sử dụng
                return ApiResponse<bool>.Fail(
                    $"Không thể xóa thể loại này vì đang được gán cho {mangaCount} truyện. " +
                    "Hãy gỡ thể loại khỏi các truyện trước.");
            }

            var success = await _categoryRepo.SoftDeleteAsync(id);
            await _categoryRepo.SaveChangesAsync();

            return success
                ? ApiResponse<bool>.Success(true, "Xóa thể loại thành công.")
                : ApiResponse<bool>.Fail("Xóa thất bại.");
        }

        // ================================================================
        // MANY-TO-MANY: TRUYỆN + CATEGORY (với Transaction)
        // ================================================================

        /// <summary>
        /// Tạo truyện mới kèm gán nhiều Category.
        /// Toàn bộ được bọc trong 1 Database Transaction (ACID).
        /// Nếu bất kỳ bước nào lỗi -> ROLLBACK toàn bộ.
        /// </summary>
        public async Task<ApiResponse<int>> CreateStoryWithCategoriesAsync(CreateStoryWithCategoriesRequest req)
        {
            // 1. Validate tên truyện
            if (string.IsNullOrWhiteSpace(req.Title))
                return ApiResponse<int>.Fail("Tên truyện không được để trống.");

            // 2. Validate Category IDs có tồn tại trong DB không (bulk check)
            if (req.CategoryIds.Any())
            {
                var allValid = await _categoryRepo.AreAllCategoryIdsValidAsync(req.CategoryIds);
                if (!allValid)
                    return ApiResponse<int>.Fail("Một hoặc nhiều ID thể loại không hợp lệ.");
            }

            // 3. Bắt đầu Transaction - Đảm bảo ACID
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // ======= BEGIN TRANSACTION =======

                // Bước 1: Tạo Story
                var story = new Story
                {
                    Title = req.Title.Trim(),
                    Slug = GenerateSlug(req.Title),
                    Author = req.Author,
                    Description = req.Description,
                    CoverImage = req.CoverImage,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Stories.Add(story);
                await _context.SaveChangesAsync(); // Phải save để lấy StoryId

                // Bước 2: Insert bảng trung gian StoryCategory (Many-to-Many)
                if (req.CategoryIds.Any())
                {
                    var storyCategories = req.CategoryIds
                        .Distinct()
                        .Select(cid => new StoryCategory
                        {
                            StoryId = story.StoryId, // ID vừa được generate
                            CategoryId = cid
                        });

                    _context.StoryCategories.AddRange(storyCategories);
                    await _context.SaveChangesAsync();
                }

                // Bước 3: COMMIT - Tất cả thành công
                await transaction.CommitAsync();
                // ======= COMMIT =======

                return ApiResponse<int>.Success(story.StoryId, $"Tạo truyện '{story.Title}' thành công.");
            }
            catch (Exception ex)
            {
                // Bước 4: ROLLBACK - Hoàn tác nếu có lỗi
                await transaction.RollbackAsync();
                // Log error (trong production nên dùng ILogger)
                return ApiResponse<int>.Fail($"Lỗi hệ thống khi tạo truyện. Đã Rollback. Chi tiết: {ex.Message}");
            }
        }

        /// <summary>
        /// Cập nhật Category Mapping cho Truyện.
        /// Logic: Xóa toàn bộ mapping cũ -> Insert mapping mới trong 1 Transaction.
        /// </summary>
        public async Task<ApiResponse<bool>> UpdateStoryCategoriesAsync(int storyId, IEnumerable<int> newCategoryIds)
        {
            var story = await _context.Stories.FindAsync(storyId);
            if (story == null || story.IsDeleted)
                return ApiResponse<bool>.Fail("Không tìm thấy truyện.");

            var categoryIdList = newCategoryIds.Distinct().ToList();

            if (categoryIdList.Any())
            {
                var allValid = await _categoryRepo.AreAllCategoryIdsValidAsync(categoryIdList);
                if (!allValid)
                    return ApiResponse<bool>.Fail("Một hoặc nhiều ID thể loại không hợp lệ.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Bước 1: Xóa toàn bộ mapping cũ
                var oldMappings = _context.StoryCategories.Where(sc => sc.StoryId == storyId);
                _context.StoryCategories.RemoveRange(oldMappings);
                await _context.SaveChangesAsync();

                // Bước 2: Insert mapping mới
                if (categoryIdList.Any())
                {
                    var newMappings = categoryIdList.Select(cid => new StoryCategory
                    {
                        StoryId = storyId,
                        CategoryId = cid
                    });
                    _context.StoryCategories.AddRange(newMappings);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                return ApiResponse<bool>.Success(true, "Cập nhật thể loại truyện thành công.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<bool>.Fail($"Lỗi khi cập nhật thể loại. Đã Rollback. Chi tiết: {ex.Message}");
            }
        }

        // ================================================================
        // HELPER: Tạo SEO Slug từ chuỗi tiếng Việt
        // ================================================================
        private static string GenerateSlug(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            // Loại bỏ dấu tiếng Việt
            string normalized = input.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in normalized)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            string slug = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();

            // Thay khoảng trắng và ký tự đặc biệt bằng dấu gạch nối
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-");
            slug = slug.Trim('-');

            return slug;
        }
    }
}
