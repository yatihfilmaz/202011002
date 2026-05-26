using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using project.Models;

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

        // Geçmiş mesajları HTML olarak değil, saf veri (JSON) olarak döndürür
        [HttpGet]
        public async Task<IActionResult> GetHistory(int orderId)
        {
            var messages = await _context.ChatMessages
                .Include(m => m.Sender)
                .Where(m => m.OrderId == orderId)
                .OrderBy(m => m.SentAt)
                .Select(m => new {
                    senderId = m.SenderId,
                    senderName = m.Sender.FullName,
                    message = m.MessageText,
                    time = m.SentAt.ToString("HH:mm")
                })
                .ToListAsync();

            return Json(messages);
        }
    }
}