    using DoAnChuyenNganh.Data;
    using DoAnChuyenNganh.Helpers;
    using DoAnChuyenNganh.Models;
    using DoAnChuyenNganh.Models.ViewModels;
    using DoAnChuyenNganh.Services;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;

    namespace DoAnChuyenNganh.Controllers
    {
        [Authorize]
        public class OrderController : Controller
        {
            private readonly AppDBContext _context;
            private readonly UserManager<User> _userManager;
            private readonly BillingService _billingService;

            public OrderController(AppDBContext context, UserManager<User> userManager, BillingService billingService)
            {
                _context = context;
                _userManager = userManager;
                _billingService = billingService;
            }
            [Authorize]
            [HttpGet]
            public async Task<IActionResult> CreateOrder(int restaurantId)
            {
                var restaurant = await _context.Restaurants
                    .Include(r => r.MenuItems)
                    .FirstOrDefaultAsync(r => r.Id == restaurantId);

                if (restaurant == null) return NotFound();

                return View(restaurant);
            }

            // ================= Khách tạo đơn =================
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> CreateOrder(int restaurantId, List<int> menuItemIds, List<int> quantities)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return RedirectToAction("Login", "Account");

                if (!menuItemIds.Any() || menuItemIds.Count != quantities.Count)
                {
                    TempData["Error"] = "Chưa chọn món ăn hoặc số lượng không hợp lệ.";
                    return RedirectToAction("Detail", "Home", new { id = restaurantId });
                }

                var items = new List<OrderItem>();
                decimal totalAmount = 0;

                for (int i = 0; i < menuItemIds.Count; i++)
                {
                    var menu = await _context.MenuItems.FindAsync(menuItemIds[i]);
                    if (menu == null) continue;

                    int quantity = quantities[i];
                    if (quantity <= 0) continue;

                    items.Add(new OrderItem
                    {
                        MenuItemId = menu.Id,
                        MenuItem = menu,
                        Quantity = quantity,
                        Price = menu.Price
                    });

                    totalAmount += menu.Price * quantity;
                }

                if (!items.Any())
                {
                    TempData["Error"] = "Không có món hợp lệ trong đơn.";
                    return RedirectToAction("Detail", "Home", new { id = restaurantId });
                }

                var order = new Order
                {
                    UserId = user.Id,
                    RestaurantId = restaurantId,
                    TotalAmount = totalAmount,
                    AdminCommission = totalAmount * 0.08m, // 8% hoa hồng Admin
                    Status = "Pending",
                    CreatedAt = DateTime.Now,
                    Items = items
                };

                HttpContext.Session.SetObject("OrderDraft", new Order
                {
                    UserId = user.Id,
                    RestaurantId = restaurantId,
                    TotalAmount = totalAmount,
                    AdminCommission = totalAmount * 0.08m,
                    Status = "Pending",
                    CreatedAt = DateTime.Now,
                    Items = items
                });

                return RedirectToAction("Detail");
            }

            // ================= Chi tiết đơn hàng =================
            [HttpGet]
            public IActionResult Detail()
            {
                var order = HttpContext.Session.GetObject<Order>("OrderDraft");
                if (order == null)
                    return RedirectToAction("MyOrders");

                var model = new OrderDetailViewModel
                {
                    Order = order,
                    DeliveryInfo = new OrderDeliveryViewModel()
                };

                return View(model);
            }

            // ================= Cập nhật thông tin giao hàng (không lưu DB) =================
            [HttpPost]
            [ValidateAntiForgeryToken]
            public IActionResult UpdateDeliveryInfo(OrderDeliveryViewModel model)
            {
                TempData["DeliveryAddress"] = model.DeliveryAddress;
                TempData["PhoneNumber"] = model.PhoneNumber;
                TempData["Note"] = model.Note;

                TempData["Success"] = "Thông tin giao hàng đã được cập nhật (chỉ hiển thị, không lưu DB)";

                return RedirectToAction("Detail", new { id = model.OrderId });
            }
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> PayCash(OrderDeliveryViewModel delivery)
            {
                var order = HttpContext.Session.GetObject<Order>("OrderDraft");
                if (order == null) return RedirectToAction("MyOrders");

                order.PaymentMethod = "Tiền mặt";
                order.Status = "Paid";
            foreach (var item in order.Items)
                {
                    if (item.MenuItem != null)
                    {
                        _context.Entry(item.MenuItem).State = EntityState.Unchanged;
                    }
                }
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
            order.Restaurant = await _context.Restaurants
    .FirstOrDefaultAsync(r => r.Id == order.RestaurantId);
            await SaveNotification(
                order.UserId,
                $"Bạn vừa đặt đơn đồ ăn ở nhà hàng {order.Restaurant?.Name}. " +
                "Staff sẽ xác nhận đơn trong 5 phút tới, bạn có thể hủy đơn trong 5 phút này nếu không sẽ không hủy được nữa."
            );

            var staffIds = await _context.StaffRestaurants
                .Where(sr => sr.RestaurantId == order.RestaurantId)
                .Select(sr => sr.UserId)
                .ToListAsync();

            foreach (var staffId in staffIds)
            {
                order.Restaurant = await _context.Restaurants
                    .FirstOrDefaultAsync(r => r.Id == order.RestaurantId);

                await SaveNotification(
                    staffId,
                    $"Bạn có đơn đặt hàng mới tại nhà hàng {order.Restaurant?.Name}."
                );
            }

            HttpContext.Session.Remove("OrderDraft");

                TempData["Success"] = "Đặt hàng thành công.";
                return RedirectToAction("MyOrders");
            }

            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> PayVnPay(OrderDeliveryViewModel delivery)
            {
                var user = await _userManager.GetUserAsync(User);
                var order = HttpContext.Session.GetObject<Order>("OrderDraft");
                if (order == null) return RedirectToAction("MyOrders");

                order.PaymentMethod = "VNPAY";

                foreach (var item in order.Items)
                {
                    if (item.MenuItem != null)
                    {
                        _context.Entry(item.MenuItem).State = EntityState.Unchanged;
                    }
                }

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
            await SaveNotification(
        order.UserId,
        $"Bạn vừa đặt đơn đồ ăn ở nhà hàng {order.Restaurant?.Name}. " +
        "Staff sẽ xác nhận đơn trong 5 phút tới, bạn có thể hủy đơn trong 5 phút này nếu không sẽ không hủy được nữa."
    );

            // Staff
            var staffIds = await _context.StaffRestaurants
                .Where(sr => sr.RestaurantId == order.RestaurantId)
                .Select(sr => sr.UserId)
                .ToListAsync();

            foreach (var staffId in staffIds)
            {
                order.Restaurant = await _context.Restaurants
                    .FirstOrDefaultAsync(r => r.Id == order.RestaurantId);

                await SaveNotification(
                    staffId,
                    $"Bạn có đơn đặt hàng mới tại nhà hàng {order.Restaurant?.Name}."
                );
            }

            HttpContext.Session.Remove("OrderDraft");

                var paymentUrl = _billingService.CreateOrderPaymentUrl(user, order, HttpContext);
                return Redirect(paymentUrl);
            }

            [HttpGet]
            public async Task<IActionResult> PaymentCallback()
            {
                var response = await _billingService.ExecuteOrderVnPayCallback(Request.Query);

                if (response.Success && response.VnPayResponseCode == "00")
                    TempData["Success"] = "Thanh toán VNPAY thành công.";
                else
                    TempData["Error"] = "Thanh toán VNPAY thất bại hoặc bị hủy.";

                return RedirectToAction("MyOrders");
            }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            var user = await _userManager.GetUserAsync(User);
            var order = await _context.Orders
                .Include(o => o.Restaurant)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null || order.UserId != user.Id)
                return NotFound();

            if (order.Status == "Confirmed")
            {
                TempData["Error"] = "Đơn hàng đã được nhà hàng xác nhận, không thể hủy.";
                return RedirectToAction("MyOrders");
            }

            if (order.Status == "Canceled")
            {
                TempData["Info"] = "Đơn hàng đã bị hủy trước đó.";
                return RedirectToAction("MyOrders");
            }

            order.Status = "Canceled";
            await _context.SaveChangesAsync();

            var staffIds = await _context.StaffRestaurants
                .Where(sr => sr.RestaurantId == order.RestaurantId)
                .Select(sr => sr.UserId)
                .ToListAsync();

            foreach (var staffId in staffIds)
            {
                await SaveNotification(
                    staffId,
                    $"❌ Khách đã hủy đơn tại nhà hàng {order.Restaurant?.Name}."
                );
            }

            TempData["Success"] = "Hủy đơn hàng thành công.";
            return RedirectToAction("MyOrders");
        }

        // ================= Danh sách đơn hàng khách =================
        [HttpGet]
            public async Task<IActionResult> MyOrders()
            {
                var user = await _userManager.GetUserAsync(User);
                var orders = await _context.Orders
                    .Where(o => o.UserId == user.Id)
                    .Include(o => o.Restaurant)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

                return View(orders);
            }

            // ================= Danh sách đơn hàng cho Staff =================
            [Authorize(Roles = "Staff")]
            [HttpGet]
            public async Task<IActionResult> ManageOrders()
            {
                var user = await _userManager.GetUserAsync(User);
                var restaurantIds = await _context.StaffRestaurants
                    .Where(sr => sr.UserId == user.Id)
                    .Select(sr => sr.RestaurantId)
                    .ToListAsync();

                var orders = await _context.Orders
                    .Where(o => restaurantIds.Contains(o.RestaurantId))
                    .Include(o => o.User)
                    .Include(o => o.Items)
                        .ThenInclude(i => i.MenuItem)
                    .Include(o => o.Restaurant)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

                return View(orders);
            }

        // ================= Staff cập nhật trạng thái đơn =================
        [Authorize(Roles = "Staff")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int orderId, string status)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return NotFound();

            // 🔒 Chỉ cho xác nhận khi đã Paid
            if (order.Status != "Paid" || status != "Confirmed")
            {
                TempData["Error"] = "Chỉ có thể xác nhận đơn hàng đã thanh toán.";
                return RedirectToAction("ManageOrders");
            }

            order.Status = "Confirmed";
            order.AdminCommission = order.TotalAmount * 0.08m;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xác nhận đơn hàng.";
            return RedirectToAction("ManageOrders");
        }

        // ================= XEM CHI TIẾT ĐƠN HÀNG ĐÃ ĐẶT =================
        [HttpGet]
            public async Task<IActionResult> OrderDetails(int id)
            {
                var user = await _userManager.GetUserAsync(User);

                var order = await _context.Orders
                    .Include(o => o.User)
                    .Include(o => o.Restaurant)
                    .Include(o => o.Items)
                        .ThenInclude(i => i.MenuItem)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (order == null)
                    return NotFound();

                // 🔒 Khách chỉ xem được đơn của chính mình
                if (User.IsInRole("Customer") && order.UserId != user.Id)
                    return Forbid();

                return View(order);
            }
        private async Task SaveNotification(string userId, string message)
        {
            var noti = new Notification
            {
                UserId = userId,
                Message = message,
                Type = "System",
                CreatedAt = DateTime.Now,
                IsRead = false
            };

            _context.Notifications.Add(noti);
            await _context.SaveChangesAsync();
        }
    }
}
