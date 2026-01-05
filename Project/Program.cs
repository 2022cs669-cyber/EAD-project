using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Project.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;

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
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (builder.Environment.IsProduction())
{
    // Try to get DATABASE_URL from environment
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL") 
                   ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
    
    if (!string.IsNullOrEmpty(databaseUrl))
    {
        // Check if it's a URL format (postgres:// or postgresql://)
        if (databaseUrl.StartsWith("postgres://") || databaseUrl.StartsWith("postgresql://"))
        {
            // Replace postgresql:// with postgres:// for Uri parsing
            databaseUrl = databaseUrl.Replace("postgresql://", "postgres://");
            
            var uri = new Uri(databaseUrl);
            var userInfo = uri.UserInfo.Split(':');
            var host = uri.Host;
            var portNum = uri.Port > 0 ? uri.Port : 5432;
            var database = uri.AbsolutePath.TrimStart('/');
            var username = userInfo[0];
            var password = userInfo.Length > 1 ? userInfo[1] : "";
            connectionString = $"Host={host};Port={portNum};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
        }
        else
        {
            // Already in Npgsql format
            connectionString = databaseUrl;
        }
    }
    
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseNpgsql(connectionString);
        // Suppress pending model changes warning for cross-database compatibility
        options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    });
    
    // Persist Data Protection keys to database for session persistence across deployments
    builder.Services.AddDataProtection()
        .PersistKeysToDbContext<ApplicationDbContext>()
        .SetApplicationName("EAD-Project");
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

// Auto-create/migrate database and seed data on startup
// Wrap in try-catch to prevent startup failures if DB is temporarily unavailable
try
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        if (app.Environment.IsProduction())
        {
            // Check if we should recreate the database to include DataProtectionKeys table
            var shouldRecreate = Environment.GetEnvironmentVariable("RECREATE_DB") == "true";
            if (shouldRecreate)
            {
                Console.WriteLine("?? RECREATE_DB is set to true. Deleting and recreating database...");
                db.Database.EnsureDeleted();
                Console.WriteLine("? Database deleted.");
            }
            
            Console.WriteLine("Creating database schema...");
            db.Database.EnsureCreated();
            Console.WriteLine("? Database schema created successfully.");
        }
        else
        {
            // For SQL Server - use migrations
            db.Database.Migrate();
        }
        
        // Seed default admin account if not exists
        if (!db.Teachers.Any(t => t.Email == "admin@example.com"))
        {
            db.Teachers.Add(new Teacher
            {
                Name = "Admin",
                Email = "admin@example.com",
                Password = "admin123"  // Change this password after first login!
            });
            db.SaveChanges();
            Console.WriteLine("? Default admin account created: admin@example.com / admin123");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"? Database initialization error (will retry on first request): {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    // Enable developer exception page in production temporarily for debugging
    app.UseDeveloperExceptionPage();
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
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
