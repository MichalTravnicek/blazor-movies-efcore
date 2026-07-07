# Blazor Web App Movies tutorial sample app

This sample app is the completed app for the Blazor Web App Movies tutorial:

[Build a Blazor movie database app (Overview)](https://learn.microsoft.com/aspnet/core/blazor/tutorials/movie-database-app/)

The app supports both **SQLite** (default) and **SQL Server** databases.  
Switch between them by changing the `DatabaseProvider` setting in `appsettings.json`.

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- For SQL Server: [SQL Server LocalDB](https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb) (included with Visual Studio)
- `dotnet-ef` tool (restored automatically via `.config/dotnet-tools.json`)

## Architecture

Each database provider has its own `DbContext` subclass so that EF Core can
maintain separate migration histories:

| Provider   | DbContext                         | Migration folder       |
|------------|-----------------------------------|------------------------|
| SQLite     | `BlazorWebAppMoviesContextSqlite` | `Migrations/Sqlite/`   |
| SQL Server | `BlazorWebAppMoviesContextSqlServer` | `Migrations/SqlServer/` |

At startup, `DbContextProvider.Create()` reads `DatabaseProvider` from config,
registers the base class factory for Identity/seed data, and the concrete
subclass factory so that `Migrate()` finds the correct migration chain.

## Configuration

The database provider is controlled by the `DatabaseProvider` key in `appsettings.json`:

```json
"DatabaseProvider": "Sqlite"      // Uses SQLite (default)
"DatabaseProvider": "SqlServer"   // Uses SQL Server
```

The app has two connection strings, one per provider:

| Connection String | Purpose                                                                     |
|---|-----------------------------------------------------------------------------|
| `BlazorWebAppMoviesContextSqlite` | SQLite: `Data Source=BlazorWebAppMovies.db`                                 |
| `BlazorWebAppMoviesContextSqlServer` | SQL Server LocalDB (`(localdb)\\MSSQLLocalDB`) or Docker (`localhost,1433`) |

The default `appsettings.json` SQL Server LocalDB connection string:

```json
    "BlazorWebAppMoviesContextSqlServer": "Server=(localdb)\\mssqllocaldb;Database=BlazorWebAppMovies;Trusted_Connection=True;MultipleActiveResultSets=true"
```
If you want SQL Server in Docker, use this connection string:

```json
"BlazorWebAppMoviesContextSqlServer": "Server=localhost,1433;Database=BlazorMovies;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
```
---

## Restore the EF Core tool

Before running any migration commands, restore the `dotnet-ef` tool:

```
dotnet tool restore
```

## Creating and applying migrations

Migrations are stored in separate subfolders based on the provider. Each must
be generated with the correct `--context` flag.

### SQLite migrations

1. Ensure `"DatabaseProvider": "Sqlite"` in `appsettings.json`
2. Generate:
   ```
   dotnet ef migrations add InitialCreate --context BlazorWebAppMoviesContextSqlite --output-dir Migrations\Sqlite
   ```
3. Apply:
   ```
   dotnet ef database update --context BlazorWebAppMoviesContextSqlite
   ```

### SQL Server migrations

1. Set `"DatabaseProvider": "SqlServer"` in `appsettings.json`
2. Generate:
   ```
   dotnet ef migrations add InitialCreate --context BlazorWebAppMoviesContextSqlServer --output-dir Migrations\SqlServer
   ```
3. Apply:
   ```
   dotnet ef database update --context BlazorWebAppMoviesContextSqlServer
   ```

> **Note:** The `--context` flag is required because EF Core needs to know which
> concrete DbContext subclass the migration belongs to. The base class
> `BlazorWebAppMoviesContext` is shared by both providers.

## Running with Docker (SQL Server)

You can run SQL Server in a Docker container instead of using LocalDB.

### Docker prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Windows) or Docker Engine

### Start the SQL Server container

```bash
cd docker

# Copy the example environment file and adjust as needed
cp .env.example .env

# Start the SQL Server container
docker compose up -d
```

The container will:
- Use the SA password from `docker/.env`
- Persist data in a named Docker volume (`sqlserver-data`)
- Listen on port `1433` (host) → `1433` (container)

To stop the container:

```bash
docker compose down
```

To stop and remove the volume (deletes all data):

```bash
docker compose down -v
```

### Update the connection string

In `appsettings.json`, change `BlazorWebAppMoviesContextSqlServer` to point to the Docker container:

```json
"BlazorWebAppMoviesContextSqlServer": "Server=localhost,1433;Database=BlazorMovies;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
```

Make sure the password matches what you set in `docker/.env`.

### Apply migrations

```bash
dotnet ef database update --context BlazorWebAppMoviesContextSqlServer
```

### Run the app

```bash
dotnet run
```

Set `"DatabaseProvider": "SqlServer"` in `appsettings.json` if it isn't already.

---

## Running the app

### Using SQLite (default)

1. Ensure `"DatabaseProvider": "Sqlite"` in `appsettings.json`
2. Generate and apply SQLite migrations (see above)
3. Run the app:
   ```
   dotnet run
   ```

### Using SQL Server (LocalDB)

1. Set `"DatabaseProvider": "SqlServer"` in `appsettings.json`
2. Generate and apply SQL Server migrations (see above)
3. Run the app:
   ```
   dotnet run
   ```

The seed data (`SeedData.Initialize`) populates the database with sample movies
and the default admin user on first run.

## Authentication & Authorization

The app uses ASP.NET Core Identity with JWT bearer tokens for authentication.
On startup, `SeedData` creates two roles and an admin user:

| Role | Description |
|------|-------------|
| `Admin` | Full access — can manage users, create/edit/delete movies |
| `User` | Read-only access — can view movies and details |

### Default admin account

| Credential | Value |
|------------|-------|
| Email | `admin@example.com` |
| Password | `Admin123!` |

### Permission matrix

| Page / Action | Unauthenticated | User role | Admin role |
|---|---|---|---|
| Movies — View list | ✅ | ✅ | ✅ |
| Movies — Details | ✅ | ✅ | ✅ |
| Movies — Create | ❌ | ✅ | ✅ |
| Movies — Edit | ❌ | ✅ | ✅ |
| Movies — Delete | ❌ | ✅ | ✅ |
| User Management | ❌ | ❌ | ✅ |

New users can register via the API endpoint (`POST /api/auth/register`) but
there is no self-service sign-up UI. User accounts must be created by an admin
through the User Management page.

### How authentication works

1. The login form on the Home page calls the JavaScript `authService.login()` function
2. This sends a POST request to `/api/auth/login` with email and password
3. On success, the server sets an `HttpOnly` + `Secure` cookie (`auth_token`) containing the JWT — the token is never exposed to JavaScript
4. The JWT middleware reads the cookie on every request and populates the `ClaimsPrincipal`
5. Logout calls `POST /api/auth/logout` which clears the cookie server-side
6. The role claim (`Admin` or `User`) is embedded in the JWT and enforced by `[Authorize]` attributes on Blazor pages

## Default URLs

- HTTPS: `https://localhost:7083`
- HTTP: `http://localhost:5216`

You can change these in `Properties/launchSettings.json`.

## Clean up

### SQLite

Delete the `BlazorWebAppMovies.db` file from the project folder.

### SQL Server (LocalDB)

1. Open **SQL Server Object Explorer** (View > SQL Server Object Explorer)
2. Navigate to `SQL Server` > `(localdb)\MSSQLLocalDB` > `Databases`
3. Right-click `BlazorWebAppMovies` and select **Delete**
4. Check **Close existing connections** and select **OK**

### SQL Server (Docker)

```bash
cd docker
docker compose down -v
```

This stops the container and removes the volume, deleting all data.