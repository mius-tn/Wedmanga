using Wedmanga.Models.DTOs;

namespace Wedmanga.Repositories.Interfaces
{
    /// <summary>
    /// Interface tầng Repository cho Category.
    /// Chỉ chứa các PRIMITIVE database operations.
    /// Business Logic KHÔNG được viết ở đây.
    /// </summary>
    public interface ICategoryRepository
    {
        // ---- READ ----
        /// <summary>Lấy toàn bộ danh sách thể loại kèm số lượng truyện</summary>
        Task<IEnumerable<CategorySummaryDto>> GetAllAsync();

        /// <summary>Lấy thể loại theo Slug, kèm danh sách truyện có phân trang</summary>
        Task<CategoryDetailDto?> GetBySlugAsync(string slug, int page, int pageSize);

        /// <summary>Lấy thể loại theo ID (trả về Entity để update/delete)</summary>
        Task<Models.Entities.Category?> FindByIdAsync(int id);

        /// <summary>Kiểm tra Slug có tồn tại không (cho validate unique)</summary>
        Task<bool> IsSlugExistsAsync(string slug);

        /// <summary>Kiểm tra các Category IDs có hợp lệ không (bulkcheck)</summary>
        Task<bool> AreAllCategoryIdsValidAsync(IEnumerable<int> ids);

        // ---- WRITE ----
        Task<Models.Entities.Category> AddAsync(Models.Entities.Category category);
        Task UpdateAsync(Models.Entities.Category category);
        Task<bool> SoftDeleteAsync(int id);
        Task SaveChangesAsync();
    }
}
