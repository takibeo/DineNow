    using DoAnChuyenNganh.Data;
    using DoAnChuyenNganh.Models;
    using DoAnChuyenNganh.Services;
using DoAnChuyenNganh.Services.VnPay;
using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;

    var builder = WebApplication.CreateBuilder(args);

    // 1️⃣ Thêm MVC
    builder.Services.AddControllersWithViews();
    // 2️⃣ Cấu hình DbContext
    builder.Services.AddDbContext<AppDBContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // 3️⃣ Cấu hình Identity
    builder.Services.AddIdentity<User, IdentityRole>(options =>
    {
        // Cấu hình password
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;

        // Cấu hình lockout
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 5;

        // Cấu hình email
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDBContext>()
    .AddDefaultTokenProviders();
    builder.Services.AddTransient<IEmailSender, EmailSender>();

    builder.Services.AddScoped<IVnPayService, VnPayService>();

    builder.Services.AddScoped<PremiumService>();
    

// 4️⃣ Thêm Authentication (cookie)
builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });

    // 5️⃣ Thêm Authorization
    builder.Services.AddAuthorization();

    var app = builder.Build();

    // 6️⃣ Seed Role mặc định
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<AppDBContext>();

        try
        {
            // ✅ Đảm bảo DB và bảng đã tồn tại
            context.Database.Migrate();

            // ✅ Sau đó mới seed Role/User
            CreateRoles(services).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Lỗi khi migrate hoặc seed dữ liệu mặc định.");
        }
    }

    // 7️⃣ Middleware
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();

    // 🔒 Bắt buộc có để đăng nhập hoạt động
    app.UseAuthentication();
    app.UseAuthorization();

    // 8️⃣ Routing mặc định
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.Run();

    // 9️⃣ Hàm tạo Role mặc định
    static async Task CreateRoles(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<User>>();

        string[] roleNames = { "Admin", "Staff", "Customer" };

        // Tạo role nếu chưa có
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }
    }

