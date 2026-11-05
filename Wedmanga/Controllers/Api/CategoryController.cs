using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wedmanga.Models.DTOs;
using Wedmanga.Services.Interfaces;

namespace Wedmanga.Controllers.Api
{
    // ================================================================
    // PUBLIC CATEGORY API - Dành cho độc giả (không cần đăng nhập)
    // Route: /api/the-loai
    // ================================================================
    [ApiController]
    [Route("api/the-loai")]
    public class CategoryController : ControllerBase
    {
        private readonly ICategoryService _categoryService;

        public CategoryController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        /// <summary>
        /// Lấy toàn bộ danh sách thể loại kèm số lượng truyện.
        /// Route: GET /api/the-loai
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<CategorySummaryDto>>), 200)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _categoryService.GetAllCategoriesAsync();
            return Ok(result);
        }

        /// <summary>
        /// Lấy danh sách truyện theo thể loại (có phân trang).
        /// Route: GET /api/the-loai/{slug}/truyen?page=1&size=20
        /// </summary>
        [HttpGet("{slug}/truyen")]
        [ProducesResponseType(typeof(ApiResponse<CategoryDetailDto>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetMangasByCategory(
            string slug,
            [FromQuery] int page = 1,
            [FromQuery] int size = 20)
        {
            var result = await _categoryService.GetMangasByCategorySlugAsync(slug, page, size);

            if (result.Status == "error")
                return NotFound(result);

            return Ok(result);
        }
    }

    // ================================================================
    // ADMIN CATEGORY API - Chỉ Admin mới được phép
    // Middleware: [Authorize(Roles = "Admin")] - JWT phải hợp lệ
    // Route: /api/quan-tri/the-loai
    // ================================================================
    [ApiController]
    [Route("api/quan-tri/the-loai")]
    [Authorize(Roles = "Admin")] // <-- JWT AuthMiddleware + RoleMiddleware
    public class AdminCategoryController : ControllerBase
    {
        private readonly ICategoryService _categoryService;

        public AdminCategoryController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        /// <summary>
        /// [ADMIN] Tạo thể loại mới.
        /// Route: POST /api/quan-tri/the-loai
        /// Body: { "name": "...", "description": "..." }
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<CategorySummaryDto>), 201)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request)
        {
            // ModelState validation (kiểm tra DataAnnotations như [Required])
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _categoryService.CreateCategoryAsync(request);

            if (result.Status == "error")
                return BadRequest(result);

            return StatusCode(201, result); // 201 Created
        }

        /// <summary>
        /// [ADMIN] Cập nhật thể loại theo ID.
        /// Route: PUT /api/quan-tri/the-loai/{id}
        /// </summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ApiResponse<CategorySummaryDto>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateCategoryRequest request)
        {
            var result = await _categoryService.UpdateCategoryAsync(id, request);

            if (result.Status == "error")
            {
                if (result.Message?.Contains("không tìm thấy") == true)
                    return NotFound(result);
                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// [ADMIN] Xóa mềm (Soft Delete) thể loại theo ID.
        /// Route: DELETE /api/quan-tri/the-loai/{id}
        /// Lưu ý: Không xóa nếu thể loại đang được gán cho truyện.
        /// </summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _categoryService.DeleteCategoryAsync(id);

            if (result.Status == "error")
            {
                if (result.Message?.Contains("Không tìm thấy") == true)
                    return NotFound(result);
                return BadRequest(result); // Đang có truyện dùng thể loại này
            }

            return Ok(result);
        }
    }

    // ================================================================
    // ADMIN STORY API (cập nhật thể loại cho truyện - Many-to-Many)
    // ================================================================
    [ApiController]
    [Route("api/quan-tri/truyen-the-loai")]
    [Authorize(Roles = "Admin,Moderator")]
    public class AdminStoryCategoryController : ControllerBase
    {
        private readonly ICategoryService _categoryService;

        public AdminStoryCategoryController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        /// <summary>
        /// [ADMIN] Tạo truyện mới kèm gán nhiều thể loại (Transaction).
        /// Route: POST /api/quan-tri/truyen-the-loai
        /// Body: { "title": "...", "categoryIds": [1, 3, 5] }
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<int>), 201)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> CreateStoryWithCategories([FromBody] CreateStoryWithCategoriesRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _categoryService.CreateStoryWithCategoriesAsync(request);

            if (result.Status == "error")
                return BadRequest(result);

            return StatusCode(201, result);
        }

        /// <summary>
        /// [ADMIN] Cập nhật thể loại cho truyện (xóa mapping cũ, insert mới - Transaction).
        /// Route: PUT /api/quan-tri/truyen-the-loai/{storyId}
        /// Body: [1, 2, 5]
        /// </summary>
        [HttpPut("{storyId:int}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> UpdateStoryCategories(int storyId, [FromBody] List<int> categoryIds)
        {
            var result = await _categoryService.UpdateStoryCategoriesAsync(storyId, categoryIds);

            if (result.Status == "error")
                return BadRequest(result);

            return Ok(result);
        }
    }
}
