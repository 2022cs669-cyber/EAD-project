# EAD Project - Deployment Guide

## PostgreSQL DataProtectionKeys Error - FIXED ?

### Problem
The application was failing with the error:
```
Npgsql.PostgresException (0x80004005): 42P01: relation "DataProtectionKeys" does not exist
```

This occurred because the Data Protection Keys table (used for session persistence across deployments) was not being created properly in PostgreSQL.

### Solution Applied

#### 1. **Updated ApplicationDbContext.cs**
- Added explicit table name configuration for `DataProtectionKeys` to ensure PostgreSQL compatibility
- Configured the entity properly with `ToTable("DataProtectionKeys")`

#### 2. **Updated Program.cs**
- Switched from `EnsureCreated()` to `Migrate()` for production environment
- Added better error handling and logging for database initialization
- Added verification check for DataProtectionKeys table
- Improved exception logging with inner exception details

#### 3. **Created EF Core Migration**
- Generated migration `AddDataProtectionKeys` to properly track schema changes
- Migration supports both SQL Server (local dev) and PostgreSQL (production)
- Migration can be applied automatically on deployment

#### 4. **Updated render.yaml**
- Changed `RECREATE_DB` from `"true"` to `"false"` to prevent database recreation on every deployment
- Database will now persist across deployments

## Deployment Instructions

### First Deployment (Fresh Database)
1. Set `RECREATE_DB` to `"true"` in `render.yaml`
2. Deploy to Render
3. After successful deployment, set `RECREATE_DB` back to `"false"`
4. Redeploy

### Subsequent Deployments
- Keep `RECREATE_DB` as `"false"`
- The application will automatically apply migrations on startup
- Database schema will be updated without data loss

### To Reset Database (if needed)
1. Set `RECREATE_DB` to `"true"` in `render.yaml`
2. Deploy
3. Set it back to `"false"`
4. Redeploy

## Environment Variables in Render

The following environment variables are configured in `render.yaml`:

| Variable | Value | Description |
|----------|-------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Sets the application environment |
| `ConnectionStrings__DefaultConnection` | From Database | Auto-populated from Render PostgreSQL database |
| `RECREATE_DB` | `false` | Set to `true` only when you want to reset the database |

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

## Troubleshooting

### If you still see DataProtectionKeys errors:
1. Check the deployment logs for migration errors
2. Verify the database connection string is correct
3. Try setting `RECREATE_DB` to `"true"` to force a fresh database

### To view logs on Render:
1. Go to your Render dashboard
2. Select your web service
3. Click on "Logs" tab
4. Look for database initialization messages

### Common Issues:
- **Connection timeout:** Database may be starting up, wait a few seconds and refresh
- **Migration errors:** Check that all migrations are committed to Git
- **Permission errors:** Verify the database user has CREATE TABLE permissions

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
??? Program.cs           # Application entry point

render.yaml              # Render deployment configuration
Dockerfile              # Docker container configuration
```

## Next Steps

1. ? Deploy the fixed code to Render
2. ? Verify the application starts without errors
3. ? Test login functionality
4. ?? Change the default admin password
5. ?? Set up proper backup procedures for the database

## Support

For issues or questions:
- Check the Render dashboard logs
- Review this deployment guide
- Check Entity Framework Core documentation for PostgreSQL
