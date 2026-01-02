using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Project.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure port for Render deployment
var port = Environment.GetEnvironmentVariable("PORT") ?? "80";
builder.WebHost.UseUrls($"http://*:{port}");

// Add services to the container
builder.Services.AddControllersWithViews();

// Add session middleware
builder.Services.AddDistributedMemoryCache(); // Required for session state
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Set session timeout duration
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Register the ApplicationDbContext for Entity Framework
// Use PostgreSQL for Production (Render), SQL Server for Development
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (builder.Environment.IsProduction())
{
    // PostgreSQL for Render deployment
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));
}
else
{
    // SQL Server for local development
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));
}

// Register email service
builder.Services.AddSingleton<Project.Services.EmailService>();

var app = builder.Build();

// Auto-migrate database on startup (useful for Render)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

// Add a Content Security Policy report-only header to detect violations without blocking.
// This is safe for development and helps find scripts that rely on eval/new Function.
app.Use(async (context, next) =>
{
    // Restrictive policy - no 'unsafe-eval' and allow scripts from self and known CDNs used.
    var cspReportOnly = "default-src 'self'; " +
                        "script-src 'self' https://code.jquery.com https://cdn.jsdelivr.net; " +
                        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
                        "img-src 'self' data:; " +
                        "connect-src 'self'; " +
                        "font-src 'self' https://cdn.jsdelivr.net;";

    context.Response.Headers["Content-Security-Policy-Report-Only"] = cspReportOnly;
    await next();
});

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Note: Remove UseHttpsRedirection for Render - it handles SSL at proxy level
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

// Enable session middleware (session needs to be before authorization)
app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
