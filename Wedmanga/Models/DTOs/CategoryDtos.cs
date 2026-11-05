using System.ComponentModel.DataAnnotations;

namespace Wedmanga.Models.DTOs
{
    // ==========================================
    // REQUEST DTOs (dữ liệu Client gửi lên)
    // ==========================================
    
    /// <summary>Dữ liệu để Tạo thể loại mới</summary>
    public class CreateCategoryRequest
    {
        [Required(ErrorMessage = "Tên thể loại không được để trống")]
        [MaxLength(100, ErrorMessage = "Tên không vượt quá 100 ký tự")]
        public string Name { get; set; } = null!;

        [MaxLength(500)]
        public string? Description { get; set; }
    }

    /// <summary>Dữ liệu để Cập nhật thể loại (partial update - optional fields)</summary>
    public class UpdateCategoryRequest
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }
    }

    /// <summary>Request tạo truyện mới kèm Category IDs (Many-to-Many)</summary>
    public class CreateStoryWithCategoriesRequest
    {
        [Required(ErrorMessage = "Tên truyện không được để trống")]
        [MaxLength(255)]
        public string Title { get; set; } = null!;

        [MaxLength(255)]
        public string? Author { get; set; }

        public string? Description { get; set; }

        public string? CoverImage { get; set; }

        /// <summary>Danh sách ID thể loại (Many-to-Many)</summary>
        public List<int> CategoryIds { get; set; } = new();
    }

    // ==========================================
    // RESPONSE DTOs (dữ liệu trả về Client)
    // ==========================================

    /// <summary>Thông tin tóm tắt của 1 thể loại (dùng trong list)</summary>
    public class CategorySummaryDto
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? Description { get; set; }
        public int MangaCount { get; set; }
    }

    /// <summary>Chi tiết thể loại kèm danh sách truyện (có phân trang)</summary>
    public class CategoryDetailDto
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? Description { get; set; }

        // Pagination info
        public int TotalMangas { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }

        public List<MangaSummaryDto> Mangas { get; set; } = new();
    }

    /// <summary>Tóm tắt 1 truyện cho danh sách</summary>
    public class MangaSummaryDto
    {
        public int StoryId { get; set; }
        public string Title { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? CoverImage { get; set; }
        public double RatingAvg { get; set; }
        public int ViewCount { get; set; }
        public string? Status { get; set; }
    }

    // ==========================================
    // WRAPPER RESPONSE (Chuẩn API Response)
    // ==========================================

    /// <summary>Chuẩn hóa API Response dùng chung</summary>
    public class ApiResponse<T>
    {
        public string Status { get; set; } = "success";
        public string? Message { get; set; }
        public T? Data { get; set; }

        public static ApiResponse<T> Success(T data, string? message = null)
            => new() { Status = "success", Data = data, Message = message };

        public static ApiResponse<T> Fail(string message)
            => new() { Status = "error", Message = message };
    }

    /// <summary>Wrapper cho Paginated response</summary>
    public class PagedApiResponse<T>
    {
        public string Status { get; set; } = "success";
        public T? Data { get; set; }
        public PaginationMeta Pagination { get; set; } = new();
    }

    public class PaginationMeta
    {
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
        public int PageSize { get; set; }
    }
}
