using Microsoft.EntityFrameworkCore;
using project.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using project.Services;
using project.Hubs;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddSignalR();

builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddDbContext<DenemeContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login"; // Kullanıcı yetkisiz bir yere girmeye çalışırsa buraya yönlendirilecek
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied"; // Yetkisi olmayan bir sayfaya (Örn: Admin paneline giren kullanıcı) girerse buraya yönlendirilecek
        options.ExpireTimeSpan = TimeSpan.FromDays(7); // Çerez 7 gün geçerli olsun
    });

builder.Services.AddSession(); // Session servisini ekle
builder.Services.AddHttpContextAccessor(); // Sepete her yerden ulaşmak için

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication(); 
app.UseAuthorization();

app.UseSession();

app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapHub<ChatHub>("/chatHub");

app.Run();