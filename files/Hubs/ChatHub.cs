using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using project.Models;
using System.Security.Claims;

namespace project.Hubs
{
    public class ChatHub : Hub
    {
        private readonly DenemeContext _context;

        public ChatHub(DenemeContext context)
        {
            _context = context;
        }

        // Kullanıcı sohbet sayfasına girdiğinde siparişe özel "Oda"ya katılır
        public async Task JoinOrderChat(string orderId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Order_{orderId}");
        }

        // Mesaj Gönderme İşlemi
        public async Task SendMessage(string orderId, string message)
        {
            var userIdStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || string.IsNullOrEmpty(message)) return;

            int senderId = int.Parse(userIdStr);
            int oid = int.Parse(orderId);

            // 1. Veritabanına Kaydet
            var chatMsg = new ChatMessage
            {
                OrderId = oid,
                SenderId = senderId,
                MessageText = message,
                SentAt = DateTime.Now
            };
            
            _context.ChatMessages.Add(chatMsg);
            await _context.SaveChangesAsync();

            // Gönderenin adını al
            var senderUser = await _context.Users.FindAsync(senderId);
            string senderName = senderUser?.FullName ?? "Bilinmiyor";

            // 2. Mesajı o siparişin odasındaki herkese (Canlı olarak) ilet
            await Clients.Group($"Order_{orderId}").SendAsync("ReceiveMessage", senderId.ToString(), senderName, message, chatMsg.SentAt.ToString("HH:mm"));
        }
    }
}