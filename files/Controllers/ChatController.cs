using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using project.Models;
using System.Security.Claims;

namespace project.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly DenemeContext _context;

        public ChatController(DenemeContext context)
        {
            _context = context;
        }
        [HttpGet]
        public async Task<IActionResult> LoadChatWidget(int orderId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int currentUserId = int.Parse(userIdStr);

            var order = await _context.Orders
                .Include(o => o.Caterer)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null || order.Status != "Tamamlandı" || (order.UserId != currentUserId && order.CatererId != currentUserId))
            {
                return BadRequest("Sohbete erişilemiyor.");
            }

            var messages = await _context.ChatMessages
                .Include(m => m.Sender)
                .Where(m => m.OrderId == orderId)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            ViewBag.OrderId = orderId;
            ViewBag.CurrentUserId = currentUserId;
            // Karşı tarafın adını belirle
            ViewBag.OtherPartyName = order.UserId == currentUserId ? order.Caterer?.FullName : order.User?.FullName;

            return PartialView("_ChatWidgetPartial", messages);
        }

        /*
        [HttpGet]
         public async Task<IActionResult> OrderChat(int orderId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int currentUserId = int.Parse(userIdStr);

            var order = await _context.Orders
                .Include(o => o.Caterer)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null) return NotFound();

            // GÜVENLİK ADIMI: Sipariş tamamlanmamışsa sohbete girişi engelle
            if (order.Status != "Tamamlandı")
            {
                return BadRequest("Canlı sohbet özelliğini kullanabilmek için siparişin tamamlanmış olması gerekmektedir.");
            }

            if (order.UserId != currentUserId && order.CatererId != currentUserId)
            {
                return Unauthorized("Bu sohbete erişim yetkiniz yok.");
            }

            var messages = await _context.ChatMessages
                .Include(m => m.Sender)
                .Where(m => m.OrderId == orderId)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            ViewBag.OrderId = orderId;
            ViewBag.CurrentUserId = currentUserId;
            ViewBag.OtherPartyName = order.UserId == currentUserId ? order.Caterer?.FullName : order.User?.FullName;

            return View(messages);
        } */
    }
}