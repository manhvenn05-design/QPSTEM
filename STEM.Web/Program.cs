using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using STEM.Web.Data;
using STEM.Web.Services;
using STEM.Web.Services.AI;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình Database Context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. Cấu hình Cookie Authentication (Phương án A)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login"; // Đường dẫn khi user chưa đăng nhập
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
    });

// Add services to the container.
builder.Services.AddMemoryCache();
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

// Cấu hình AI Service
builder.Services.Configure<GoogleAiOptions>(builder.Configuration.GetSection(GoogleAiOptions.Position));
builder.Services.AddHttpClient<IAIService, GeminiAiService>();

builder.Services.AddScoped<IAdminShellService, AdminShellService>();
builder.Services.AddScoped<ITeacherShellService, TeacherShellService>();
builder.Services.AddScoped<AttendanceWorkflowService>();
builder.Services.AddScoped<PayrollCalculationService>();
builder.Services.AddSingleton<IFileStorageService, LocalFileStorageService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication(); // Bắt buộc phải đứng trước UseAuthorization
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
