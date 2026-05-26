using Microsoft.AspNetCore.SignalR;
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

        // Kullanıcıyı (Müşteri veya Restoran) o siparişe ait özel canlı odaya bağlar
        public async Task JoinOrder(string orderId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Order_{orderId}");
        }

        // Mesajı veritabanına kaydeder ve odadaki taraflara anlık olarak yayınlar
        public async Task SendMessage(string orderId, string message)
        {
            var userIdStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || string.IsNullOrEmpty(message)) return;

            int senderId = int.Parse(userIdStr);
            int oid = int.Parse(orderId);

            var chatMsg = new ChatMessage
            {
                OrderId = oid,
                SenderId = senderId,
                MessageText = message,
                SentAt = DateTime.Now
            };
            
            _context.ChatMessages.Add(chatMsg);
            await _context.SaveChangesAsync();

            var senderUser = await _context.Users.FindAsync(senderId);
            string senderName = senderUser?.FullName ?? "Bilinmiyor";

            // Odadaki herkese mesajı ilet (orderId, senderId, senderName, message, time)
            await Clients.Group($"Order_{orderId}").SendAsync("ReceiveMessage", orderId, senderId, senderName, message, chatMsg.SentAt.ToString("HH:mm"));
        }
    }
}