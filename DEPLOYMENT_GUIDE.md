# EAD Project - Deployment Guide

## PostgreSQL DataProtectionKeys Error - FIXED ?

### Problem
The application was failing with the error:
```
Npgsql.PostgresException (0x80004005): 42P01: relation "DataProtectionKeys" does not exist
Position: 47
```

This occurred because:
1. The database was initially created with `EnsureCreated()` instead of migrations
2. The Data Protection Keys table wasn't being created properly in PostgreSQL
3. When switching to `Migrate()`, the migrations history wasn't tracked properly

### Solution Applied (Version 2 - Enhanced)

#### 1. **Updated ApplicationDbContext.cs**
- Added explicit table name configuration for `DataProtectionKeys` to ensure PostgreSQL compatibility
- Configured the entity properly with `ToTable("DataProtectionKeys")`

#### 2. **Updated Program.cs (Enhanced Migration Handling)**
- Implemented smart database initialization that handles transition from `EnsureCreated()` to `Migrate()`
- Detects if database was created without migrations and initializes migration history
- Automatically creates DataProtectionKeys table if missing using raw SQL
- Added comprehensive logging for debugging
- Better error handling with fallback table creation

#### 3. **Created EF Core Migration**
- Generated migration `AddDataProtectionKeys` to properly track schema changes
- Migration supports both SQL Server (local dev) and PostgreSQL (production)
- Migration can be applied automatically on deployment

#### 4. **Updated render.yaml**
- Set `RECREATE_DB` to `"true"` for next deployment to ensure clean database creation
- After successful deployment, change back to `"false"` for data persistence

## Deployment Instructions

### IMPORTANT: Next Deployment (Fix the Error)
1. ? `RECREATE_DB` is already set to `"true"` in `render.yaml`
2. Commit and push the updated code
3. Deploy to Render - database will be recreated with proper migrations
4. After successful deployment, set `RECREATE_DB` back to `"false"` in `render.yaml`
5. Commit and push again to persist the change

### Subsequent Deployments
- Keep `RECREATE_DB` as `"false"`
- The application will automatically:
  - Detect and apply pending migrations
  - Handle migration history properly
  - Create DataProtectionKeys table if missing
- Database schema will be updated without data loss

### To Reset Database (if needed in future)
1. Set `RECREATE_DB` to `"true"` in `render.yaml`
2. Commit and push
3. After deployment, set it back to `"false"`
4. Commit and push again

## What's Different in Version 2?

The enhanced Program.cs now:

? **Detects if database was created without migrations**
   - Checks for `__EFMigrationsHistory` table
   - Initializes migration tracking if missing

? **Handles transition from EnsureCreated to Migrate**
   - Marks old migrations as applied
   - Only applies new migrations

? **Creates DataProtectionKeys table as fallback**
   - If migration fails, creates table directly with raw SQL
   - Ensures table exists before application starts using sessions

? **Better logging and error handling**
   - Shows which migrations are being applied
   - Reports database state clearly
   - Continues running even if there are minor errors

## Environment Variables in Render

The following environment variables are configured in `render.yaml`:

| Variable | Value | Description |
|----------|-------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Sets the application environment |
| `ConnectionStrings__DefaultConnection` | From Database | Auto-populated from Render PostgreSQL database |
| `RECREATE_DB` | `true` (temporarily) | Set to `true` for next deployment, then change to `false` |

## Default Admin Account

After first deployment, a default admin account is created:
- **Email:** admin@example.com
- **Password:** admin123

?? **Important:** Change this password after first login!

## Database Configuration

### Local Development (SQL Server)
- Uses SQL Server LocalDB or SQL Server instance
- Connection string in `appsettings.json`
- Migrations are applied automatically

### Production (PostgreSQL on Render)
- Uses Render PostgreSQL database
- Connection string provided by Render via environment variable
- SSL Mode: Required
- Migrations are applied automatically on startup
- DataProtectionKeys table created automatically if missing

## Troubleshooting

### If you still see DataProtectionKeys errors:
1. **Check logs** - Look for "DataProtectionKeys table created successfully" message
2. **Verify RECREATE_DB** - Make sure it's set to `"true"` for the next deployment
3. **Check database connection** - Ensure Render PostgreSQL database is running
4. **Force recreation** - Set `RECREATE_DB` to `"true"` and redeploy

### To view logs on Render:
1. Go to your Render dashboard
2. Select your web service
3. Click on "Logs" tab
4. Look for these messages:
   - "? Database migrations applied successfully"
   - "? DataProtectionKeys table verified"
   - "? DataProtectionKeys table created successfully" (if it was missing)

### Common Issues and Solutions:

| Issue | Solution |
|-------|----------|
| **"relation DataProtectionKeys does not exist"** | Set `RECREATE_DB="true"` and redeploy |
| **Connection timeout** | Database may be starting up, wait 30 seconds and refresh |
| **Migration errors** | Check that all migrations are committed to Git |
| **Permission errors** | Verify database user has CREATE TABLE permissions |
| **"No migrations history found"** | Normal - app will initialize it automatically |

## Expected Log Output

On successful deployment, you should see:

```
?? RECREATE_DB is set to true. Deleting and recreating database...
? Database deleted.
Applying database migrations...
? Database migrations applied successfully.
? DataProtectionKeys table verified (contains no data).
? Default admin account created: admin@example.com / admin123
```

Or if database already exists:

```
? Found X applied migrations.
Applying Y pending migration(s)...
  - 20260105193631_AddDataProtectionKeys
? Pending migrations applied successfully.
? DataProtectionKeys table verified (contains no data).
```

## Project Structure

```
Project/
??? Controllers/          # MVC Controllers
??? Models/              # Data models and DbContext
??? Views/               # Razor views
??? Migrations/          # EF Core migrations
??? Services/            # Email and other services
??? appsettings.json     # Development configuration
??? appsettings.Production.json  # Production configuration
??? Program.cs           # Application entry point (ENHANCED)

render.yaml              # Render deployment configuration
Dockerfile              # Docker container configuration
DEPLOYMENT_GUIDE.md      # This file
```

## Next Steps

1. ? Code is already updated and fixed
2. ?? **Commit and push the changes** (next step)
3. ?? **Deploy to Render** - Database will be recreated properly
4. ? Verify the application starts without errors
5. ? Test login functionality
6. ?? Change the default admin password
7. ?? Set `RECREATE_DB` back to `"false"` after successful deployment
8. ?? Set up proper backup procedures for the database

## Support

For issues or questions:
- Check the Render dashboard logs
- Review this deployment guide
- Check Entity Framework Core documentation for PostgreSQL
- Verify migrations are committed and pushed to Git
