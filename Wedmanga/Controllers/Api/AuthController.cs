using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Wedmanga.Data;
using Wedmanga.Models.Entities;
using BCrypt.Net;

namespace Wedmanga.Controllers.Api
{
    [Route("api/xac-thuc")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly MangaDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(MangaDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        public class RegisterRequest
        {
            public string Username { get; set; } = null!;
            public string Email { get; set; } = null!;
            public string Password { get; set; } = null!;
        }

        [HttpPost("dang-ky")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            if (await _context.Users.AnyAsync(u => u.Email == req.Email || u.Username == req.Username))
                return BadRequest(new { message = "Email hoặc Username đã tồn tại." });

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "User");
            if (role == null) return StatusCode(500, "Role hệ thống chưa sẵn sàng.");

            var user = new User
            {
                Username = req.Username,
                Email = req.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
                RoleId = role.RoleId
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đăng ký thành công." });
        }

        public class LoginRequest
        {
            public string Identifier { get; set; } = null!; // username or email
            public string Password { get; set; } = null!;
        }

        [HttpPost("dang-nhap")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == req.Identifier || u.Username == req.Identifier);

            if (user == null || user.IsDeleted || user.Status != "Active")
                return Unauthorized(new { message = "Tài khoản không tồn tại hoặc bị khóa." });

            if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
                return Unauthorized(new { message = "Sai mật khẩu." });

            var token = GenerateJwt(user);
            return Ok(new { token, user = new { user.UserId, user.Username, user.Email, user.Role.RoleName } });
        }

        private string GenerateJwt(User user)
        {
            var jwtSettings = _config.GetSection("JwtSettings");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Secret"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role.RoleName)
            };

            var token = new JwtSecurityToken(
                jwtSettings["Issuer"],
                jwtSettings["Audience"],
                claims,
                expires: DateTime.UtcNow.AddMinutes(double.Parse(jwtSettings["ExpiryMinutes"]!)),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
