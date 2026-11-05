using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Wedmanga.Data;
using Wedmanga.Repositories;
using Wedmanga.Repositories.Interfaces;
using Wedmanga.Services;
using Wedmanga.Services.Interfaces;

namespace Wedmanga
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            
            // Add DbContext
            builder.Services.AddDbContext<MangaDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // === DI : Repository & Service (Scoped = 1 instance per HTTP request) ===
            builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
            builder.Services.AddScoped<ICategoryService, CategoryService>();

            // === Authentication: Cookie (MVC) + JWT Bearer (API) ===
            var jwtSettings = builder.Configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["Secret"];

            builder.Services.AddAuthentication(options =>
            {
                // MVC controllers dùng Cookie là default
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            // Cookie Auth cho MVC Views (/Admin, /Account)
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.LoginPath = "/tai-khoan/dang-nhap";
                options.LogoutPath = "/tai-khoan/dang-xuat";
                options.AccessDeniedPath = "/tai-khoan/dang-nhap";
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.SlidingExpiration = true;
            })
            // JWT Bearer cho API controllers (/api/...)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey ?? ""))
                };
            });

            var app = builder.Build();

// Seed Database on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<MangaDbContext>();
        DbInitializer.Initialize(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred creating the DB.");
    }
}

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();
            
            app.UseAuthentication(); // Must be before Authorization
            app.UseAuthorization();

            app.MapStaticAssets();

            // Route cho trang truyện: /truyen/{slug}
            app.MapControllerRoute(
                name: "storyDetail",
                pattern: "truyen/{slug}",
                defaults: new { controller = "Truyen", action = "Details" });

            // Route cho trang đọc chương: /truyen/{storySlug}/chuong/{chapterNumber}
            app.MapControllerRoute(
                name: "chapterRead",
                pattern: "truyen/{storySlug}/chuong/{chapterNumber:int}",
                defaults: new { controller = "Truyen", action = "Read" });

            // Route cho thể loại: /the-loai/{slug}
            app.MapControllerRoute(
                name: "category",
                pattern: "the-loai/{slug}",
                defaults: new { controller = "Truyen", action = "Category" });

            // Route cho kiểu truyện (Manga / Light Novel)
            app.MapControllerRoute(
                name: "truyenType",
                pattern: "The-Loai-Truyen/{type}",
                defaults: new { controller = "Truyen", action = "Type" });

            // Route cho tài khoản
            app.MapControllerRoute(
                name: "login",
                pattern: "tai-khoan/dang-nhap",
                defaults: new { controller = "Account", action = "Login" });

            app.MapControllerRoute(
                name: "register",
                pattern: "tai-khoan/dang-ky",
                defaults: new { controller = "Account", action = "Register" });

            app.MapControllerRoute(
                name: "logout",
                pattern: "tai-khoan/dang-xuat",
                defaults: new { controller = "Account", action = "Logout" });

            app.MapControllerRoute(
                name: "profile",
                pattern: "tai-khoan/ho-so",
                defaults: new { controller = "Account", action = "Profile" });

            app.MapControllerRoute(
                name: "changePassword",
                pattern: "tai-khoan/doi-mat-khau",
                defaults: new { controller = "Account", action = "ChangePassword" });

            // Route mặc định
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}
