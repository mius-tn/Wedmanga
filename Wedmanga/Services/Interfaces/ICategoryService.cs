using Wedmanga.Models.DTOs;

namespace Wedmanga.Services.Interfaces
{
    /// <summary>
    /// Interface tầng Service cho Category.
    /// Chứa Business Logic, Validation nghiệp vụ, Slug generation, etc.
    /// </summary>
    public interface ICategoryService
    {
        // -- PUBLIC API (không cần quyền) --

        /// <summary>Lấy toàn bộ thể loại cho trang web (public)</summary>
        Task<ApiResponse<IEnumerable<CategorySummaryDto>>> GetAllCategoriesAsync();

        /// <summary>Lấy chi tiết thể loại + danh sách truyện có phân trang (public)</summary>
        Task<ApiResponse<CategoryDetailDto>> GetMangasByCategorySlugAsync(string slug, int page = 1, int pageSize = 20);

        // -- ADMIN API (cần quyền Admin) --

        /// <summary>Tạo thể loại mới - validate slug unique, auto-generate slug từ name</summary>
        Task<ApiResponse<CategorySummaryDto>> CreateCategoryAsync(CreateCategoryRequest request);

        /// <summary>Cập nhật thể loại theo ID</summary>
        Task<ApiResponse<CategorySummaryDto>> UpdateCategoryAsync(int id, UpdateCategoryRequest request);

        /// <summary>Xóa mềm (soft delete) thể loại theo ID</summary>
        Task<ApiResponse<bool>> DeleteCategoryAsync(int id);

        // -- STORY với CATEGORY (Many-to-Many) --

        /// <summary>
        /// Tạo truyện mới kèm gán Category IDs trong 1 Transaction.
        /// Nếu lỗi ở bất kỳ bước nào -> ROLLBACK toàn bộ.
        /// </summary>
        Task<ApiResponse<int>> CreateStoryWithCategoriesAsync(CreateStoryWithCategoriesRequest request);

        /// <summary>
        /// Cập nhật mapping Category cho Truyện (xóa toàn bộ mapping cũ -> insert mới).
        /// Dùng Transaction để đảm bảo ACID.
        /// </summary>
        Task<ApiResponse<bool>> UpdateStoryCategoriesAsync(int storyId, IEnumerable<int> newCategoryIds);
    }
}
