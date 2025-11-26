using System.Security.Claims;
using DoAnChuyenNganh.Data;
using DoAnChuyenNganh.Models;
using DoAnChuyenNganh.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using DoAnChuyenNganh.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace DoAnChuyenNganh.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDBContext _context;
        private readonly ILogger<AccountController> _logger;
        private readonly IEmailSender _emailSender;

        public AccountController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            RoleManager<IdentityRole> roleManager,
            AppDBContext context,
            ILogger<AccountController> logger,
            IEmailSender emailSender) // inject EmailSender
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _context = context;
            _logger = logger;
            _emailSender = emailSender;
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new User
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName,
                    Address = model.Address,
                    DateOfBirth = model.DateOfBirth,
                    PhoneNumber = model.PhoneNumber,
                    IsActive = true
                };


                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Customer");

                    await SaveUserLog(user.Id, "Register", "Đăng ký tài khoản mới");
                    _logger.LogInformation($"Người dùng {user.Email} đăng ký thành công.");

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("Index", "Home");
                }

                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(
                    model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    var user = await _userManager.FindByEmailAsync(model.Email);
                    await SaveUserLog(user.Id, "Login", "Đăng nhập thành công");
                    _logger.LogInformation($"Người dùng {model.Email} đăng nhập thành công.");
                    return RedirectToAction("Index", "Home");
                }

                ModelState.AddModelError("", "Sai email hoặc mật khẩu!");
            }
            return View(model);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = User.Identity?.Name;

            await SaveUserLog(userId, "Logout", "Đăng xuất khỏi hệ thống");
            await _signInManager.SignOutAsync();

            _logger.LogInformation($"Người dùng {email} đã đăng xuất.");
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied() => View();

        // 🧠 Ghi log hành vi người dùng (đơn giản, không có IP)
        private async Task SaveUserLog(string userId, string action, string? description = null)
        {
            var log = new UserLog
            {
                UserId = userId,
                Action = action,
                Description = description ?? $"{action} thành công",
                Timestamp = DateTime.Now
            };
            _context.UserLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        [Authorize]
        public async Task<IActionResult> ActivityHistory()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var logs = await _context.UserLogs
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();

            return View(logs);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var model = new EditProfileViewModel
            {
                FullName = user.FullName,
                Address = user.Address,
                DateOfBirth = user.DateOfBirth,
                PhoneNumber = user.PhoneNumber // Lấy luôn PhoneNumber
            };

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(EditProfileViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            // Cập nhật thông tin cơ bản
            user.FullName = model.FullName;
            user.Address = model.Address;
            user.DateOfBirth = model.DateOfBirth;

            // Cập nhật PhoneNumber bằng Identity method
            var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, model.PhoneNumber);

            var updateResult = await _userManager.UpdateAsync(user);

            if (updateResult.Succeeded && setPhoneResult.Succeeded)
            {
                await SaveUserLog(user.Id, "UpdateProfile", "Người dùng cập nhật thông tin cá nhân");
                ViewBag.Message = "✅ Cập nhật thông tin thành công!";
            }
            else
            {
                var errors = updateResult.Errors.Concat(setPhoneResult.Errors)
                                .Select(e => e.Description);
                ViewBag.Message = "❌ Không thể cập nhật: " + string.Join("<br/>", errors);
            }

            // 🔑 Nếu người dùng nhập mật khẩu mới thì đổi mật khẩu
            if (!string.IsNullOrEmpty(model.OldPassword) &&
                !string.IsNullOrEmpty(model.NewPassword) &&
                !string.IsNullOrEmpty(model.ConfirmPassword))
            {
                var passwordResult = await _userManager.ChangePasswordAsync(
                    user, model.OldPassword, model.NewPassword);

                if (passwordResult.Succeeded)
                {
                    await _signInManager.RefreshSignInAsync(user);
                    await SaveUserLog(user.Id, "ChangePassword", "Người dùng đổi mật khẩu");
                    ViewBag.PasswordMessage = "✅ Đổi mật khẩu thành công!";
                }
                else
                {
                    ViewBag.PasswordError = string.Join("<br/>", passwordResult.Errors.Select(e => e.Description));
                }
            }

            // Trả lại model với PhoneNumber đã cập nhật (không bị mất dữ liệu khi có lỗi)
            model.PhoneNumber = user.PhoneNumber;

            return View(model);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> ClearAllLogs()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var logs = _context.UserLogs.Where(l => l.UserId == userId);

            _context.UserLogs.RemoveRange(logs);
            await _context.SaveChangesAsync();

            await SaveUserLog(userId, "ClearLogs", "Xóa toàn bộ lịch sử hoạt động");
            return RedirectToAction(nameof(ActivityHistory));
        }

        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user != null)
            {
                // 🔐 Tạo mật khẩu mới ngẫu nhiên
                string newPassword = Guid.NewGuid().ToString("N").Substring(0, 8) + "aA!";

                // 🪪 Tạo token reset và reset mật khẩu
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetResult = await _userManager.ResetPasswordAsync(user, token, newPassword);

                if (resetResult.Succeeded)
                {
                    // 📂 Đọc template HTML từ file wwwroot/email-templates/ForgotPassword.html
                    string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/email-templates/ForgotPassword.cshtml");
                    string template = System.IO.File.ReadAllText(templatePath);

                    // 🎨 Thay thế các placeholder
                    string emailBody = template
                        .Replace("{FULL_NAME}", user.FullName)
                        .Replace("{NEW_PASSWORD}", newPassword);

                    // ✉ Gửi email
                    await _emailSender.SendEmailAsync(
                        user.Email,
                        "Mật khẩu mới của bạn",
                        emailBody
                    );
                }
            }

            // 🛑 Không tiết lộ email tồn tại hay chưa
            ViewBag.Message = "Mật khẩu mới đã được gửi! Vui lòng kiểm tra hộp thư.";
            return View(model);
        }


    }
}
