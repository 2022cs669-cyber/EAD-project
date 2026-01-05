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
    
    // CRITICAL: Create DataProtectionKeys table BEFORE configuring Data Protection
    // This ensures the table exists when Data Protection system tries to use it
    Console.WriteLine("Pre-initializing database for Data Protection...");
    try
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        
        using (var tempDb = new ApplicationDbContext(optionsBuilder.Options))
        {
            // Only ensure database exists, don't create all tables yet
            try
            {
                var canConnect = tempDb.Database.CanConnect();
                if (!canConnect)
                {
                    Console.WriteLine("Database doesn't exist yet, will be created during migration.");
                }
            }
            catch
            {
                Console.WriteLine("Database connection check skipped.");
            }
            
            // Ensure DataProtectionKeys table exists
            try
            {
                var exists = tempDb.DataProtectionKeys.Any();
                Console.WriteLine("? DataProtectionKeys table already exists.");
            }
            catch
            {
                Console.WriteLine("Creating DataProtectionKeys table...");
                try
                {
                    tempDb.Database.ExecuteSqlRaw(@"
                        CREATE TABLE IF NOT EXISTS ""DataProtectionKeys"" (
                            ""Id"" SERIAL PRIMARY KEY,
                            ""FriendlyName"" TEXT,
                            ""Xml"" TEXT
                        );
                    ");
                    Console.WriteLine("? DataProtectionKeys table created.");
                }
                catch (Exception createEx)
                {
                    Console.WriteLine($"?? Could not create DataProtectionKeys table yet: {createEx.Message}");
                    Console.WriteLine("Will be created during migration phase.");
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"?? Pre-initialization warning: {ex.Message}");
    }
    
    // NOW configure Data Protection (table should exist by now)
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
            // Check if we should recreate the database
            var shouldRecreate = Environment.GetEnvironmentVariable("RECREATE_DB") == "true";
            if (shouldRecreate)
            {
                Console.WriteLine("?? RECREATE_DB is set to true. Deleting and recreating database...");
                db.Database.EnsureDeleted();
                Console.WriteLine("? Database deleted.");
                
                // After deletion, apply migrations to create fresh schema
                Console.WriteLine("Applying database migrations...");
                db.Database.Migrate();
                Console.WriteLine("? Database migrations applied successfully.");
                
                // Ensure DataProtectionKeys exists after migration
                try
                {
                    db.Database.ExecuteSqlRaw(@"
                        CREATE TABLE IF NOT EXISTS ""DataProtectionKeys"" (
                            ""Id"" SERIAL PRIMARY KEY,
                            ""FriendlyName"" TEXT,
                            ""Xml"" TEXT
                        );
                    ");
                    Console.WriteLine("? DataProtectionKeys table verified after migration.");
                }
                catch (Exception dpEx)
                {
                    Console.WriteLine($"?? DataProtectionKeys table creation: {dpEx.Message}");
                }
            }
            else
            {
                // Check if database exists
                var canConnect = db.Database.CanConnect();
                if (!canConnect)
                {
                    Console.WriteLine("Database doesn't exist. Creating with migrations...");
                    db.Database.Migrate();
                    Console.WriteLine("? Database created with migrations.");
                }
                else
                {
                    // Database exists, check if it was created with EnsureCreated or Migrate
                    var hasMigrationsTable = false;
                    try
                    {
                        // Try to query the migrations history table
                        var appliedMigrations = db.Database.GetAppliedMigrations();
                        hasMigrationsTable = true;
                        Console.WriteLine($"? Found {appliedMigrations.Count()} applied migrations.");
                    }
                    catch
                    {
                        Console.WriteLine("?? No migrations history found. Database was created with EnsureCreated().");
                        hasMigrationsTable = false;
                    }
                    
                    if (!hasMigrationsTable)
                    {
                        // Database was created with EnsureCreated, need to initialize migrations table
                        Console.WriteLine("Initializing migrations history...");
                        
                        // Create the __EFMigrationsHistory table manually for PostgreSQL
                        try
                        {
                            db.Database.ExecuteSqlRaw(@"
                                CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                                    ""MigrationId"" character varying(150) NOT NULL,
                                    ""ProductVersion"" character varying(32) NOT NULL,
                                    CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY (""MigrationId"")
                                );
                            ");
                            Console.WriteLine("? Migrations history table created.");
                            
                            // Get all migrations from the assembly
                            var allMigrations = db.Database.GetMigrations().ToList();
                            Console.WriteLine($"Found {allMigrations.Count} total migrations in assembly.");
                            
                            // Mark all migrations except the last one as applied
                            // (assuming database was created with EnsureCreated before the last migration)
                            if (allMigrations.Count > 0)
                            {
                                var migrationsToMark = allMigrations.Take(allMigrations.Count - 1);
                                foreach (var migration in migrationsToMark)
                                {
                                    db.Database.ExecuteSqlRaw(
                                        @"INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"") 
                                          VALUES ({0}, {1}) 
                                          ON CONFLICT (""MigrationId"") DO NOTHING;",
                                        migration,
                                        "9.0.1");
                                    Console.WriteLine($"  Marked migration as applied: {migration}");
                                }
                            }
                            
                            Console.WriteLine("? Migrations history initialized.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"?? Error initializing migrations: {ex.Message}");
                        }
                    }
                    
                    // Now apply any pending migrations
                    var pendingMigrations = db.Database.GetPendingMigrations().ToList();
                    if (pendingMigrations.Any())
                    {
                        Console.WriteLine($"Applying {pendingMigrations.Count} pending migration(s)...");
                        foreach (var migration in pendingMigrations)
                        {
                            Console.WriteLine($"  - {migration}");
                        }
                        db.Database.Migrate();
                        Console.WriteLine("? Pending migrations applied successfully.");
                    }
                    else
                    {
                        Console.WriteLine("? Database is up to date. No pending migrations.");
                    }
                }
            }
            
            // Final verification: Ensure DataProtectionKeys table exists
            try
            {
                var hasKeys = db.DataProtectionKeys.Any();
                Console.WriteLine($"? DataProtectionKeys table verified (contains {(hasKeys ? "data" : "no data")}).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"?? DataProtectionKeys table missing: {ex.Message}");
                Console.WriteLine("Creating DataProtectionKeys table as fallback...");
                
                try
                {
                    // Create the table explicitly for PostgreSQL
                    db.Database.ExecuteSqlRaw(@"
                        CREATE TABLE IF NOT EXISTS ""DataProtectionKeys"" (
                            ""Id"" SERIAL PRIMARY KEY,
                            ""FriendlyName"" TEXT,
                            ""Xml"" TEXT
                        );
                    ");
                    Console.WriteLine("? DataProtectionKeys table created successfully.");
                    
                    // Verify again
                    var hasKeys = db.DataProtectionKeys.Any();
                    Console.WriteLine($"? DataProtectionKeys table verified (contains {(hasKeys ? "data" : "no data")}).");
                }
                catch (Exception createEx)
                {
                    Console.WriteLine($"? Failed to create DataProtectionKeys table: {createEx.Message}");
                }
            }
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
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
    }
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
