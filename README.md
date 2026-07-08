# Blazor Web App Movies sample app

[![.NET](https://github.com/MichalTravnicek/blazor-movies-efcore/actions/workflows/dotnet.yml/badge.svg)](https://github.com/MichalTravnicek/blazor-movies-efcore/actions/workflows/dotnet.yml)

This sample app is showcasing .NET Framework with Blazor UI and EF Core ORM + migrations for SQL Server and SQLite:


Based on [Build a Blazor movie database app (Overview)](https://learn.microsoft.com/aspnet/core/blazor/tutorials/movie-database-app/)

The app supports both **SQLite** (default) and **SQL Server** databases.  
Switch between them by changing the `DatabaseProvider` setting in `appsettings.json`.

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- For SQL Server: [SQL Server LocalDB](https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb) (included with Visual Studio)
- `dotnet-ef` tool (restored automatically via `.config/dotnet-tools.json`)

## Architecture

The app has a **dual UI architecture** — Blazor Interactive Server and Classic UI (Razor Pages + jQuery) — sharing the same backend.

```
┌──────────────────────────────────────────────────────────────────────────┐
│                           BlazorWebAppMovies                             │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   ┌───────────────────────────┐          ┌───────────────────────────┐   │
│   │         Blazor UI         │          │        Classic UI         │   │
│   │   (Interactive Server)    │          │  (Razor Pages + jQuery)   │   │
│   ├───────────────────────────┤          ├───────────────────────────┤   │
│   │  • DbContextFactory       │          │  • Page Models            │   │
│   │  • UserManager            │          │  • UserManager            │   │
│   │  • SignalR (client)       │          │  • jQuery AJAX            │   │
│   └─────────────┬─────────────┘          └─────────────┬─────────────┘   │
│                 │                                      │                 │
│                 │                                      ▼                 │
│                 │                        ┌───────────────────────────┐   │
│                 │                        │      API Controllers      │   │
│                 │                        │   (Movies, Auth, Admin)   │   │
│                 │                        └─────────────┬─────────────┘   │
│                 │                                      │                 │
│                 ▼                                      ▼                 │
│   ┌──────────────────────────────────────────────────────────────────┐   │
│   │                        Database (SQLite)                         │   │
│   └──────────────────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────────────┘

```

For detailed documentation, see:
- [UI Architecture Guide](docs/ui-architecture-guide.md)
- [Backend Architecture Guide](docs/backend-architecture-guide.md)

### Database Providers

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
| Classic UI — Movies (`/classic/movies`) | ✅ | ✅ | ✅ |
| Classic UI — Users (`/classic/users`) | ❌ | ❌ | ✅ |

New users can register via the API endpoint (`POST /api/auth/register`) but
there is no self-service sign-up UI. User accounts must be created by an admin
through the User Management page (Blazor or Classic UI).

### Classic UI (jQuery)

The app also provides a **Classic UI** built with Razor Pages + jQuery + DataTables.
It shares the same API controllers, JWT auth, and database as the Blazor UI.

| Feature | Classic UI URL |
|---|---|
| Movies (public) | `/classic/movies` |
| User Management (admin only) | `/classic/users` |

The Classic UI has its own login modal, navbar, and CRUD modals for both movies and users.
Switch between UIs using the nav link in the Classic UI, or the "Choose Your UI"
card on the Blazor Home page. Your preference is stored in a cookie.

### User Management Features

Both UIs support full user management for admins:

| Feature | Blazor UI (`/usermanagement`) | Classic UI (`/classic/users`) |
|---------|-------------------------------|-------------------------------|
| List users | ✅ | ✅ |
| Create user | ✅ | ✅ |
| Edit user (name, email, role) | ✅ | ✅ |
| Change password | ✅ | ✅ |
| Delete user | ✅ (self-protected) | ✅ (self-protected) |

Admins can edit their own account but cannot delete themselves.

## Test Suite

The project contains **245 unit tests** across multiple test categories:

| Category | Tests | What it covers |
|---|---|---|
| Controllers | 63 | `AuthController`, `MoviesController`, `AdminController` |
| Classic UI Pages | 23 | Page model data binding, auth state, JSON serialization |
| Blazor UI (service layer) | 51 | Movie CRUD, user management, auth flow, role guards |
| Database / Models | ~70 | Entity validation, queries, context, seed data |
| DTOs / Mapping | 27 | DTO validation, AutoMapper profiles |

Run all tests:
```
dotnet test
```

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

---

## Movies API

The app exposes a RESTful JSON API for managing movies using **AutoMapper** for entity-to-DTO mapping.

### Endpoints

| Method | Route | Auth | Description |
|---|---|---|---|
| `GET` | `/api/movies` | Public | List all movies (ordered by release date) |
| `GET` | `/api/movies/{id}` | Public | Get a single movie by ID |
| `POST` | `/api/movies` | Required | Create a new movie |
| `PUT` | `/api/movies/{id}` | Required | Update an existing movie |
| `DELETE` | `/api/movies/{id}` | Required | Delete a movie |

### DTOs

| DTO | Direction | Purpose |
|---|---|---|
| `MovieDto` | Output | Full movie data including Id |
| `CreateMovieDto` | Input | Create a movie (validated) |
| `UpdateMovieDto` | Input | Update a movie (validated) |

All input DTOs carry the same validation rules as the `Movie` entity — title (3-60 chars, no whitespace-only), genre (uppercase-starting), rating (G/PG/PG-13/R/NC-17), and price (0-100).

### AutoMapper

The `MovieProfile` class in `Models/Mapping/` defines three mappings:

- `Movie → MovieDto` — all properties, including Id
- `CreateMovieDto → Movie` — Id is ignored (DB-generated)
- `UpdateMovieDto → Movie` — Id is ignored (preserved from existing entity)

AutoMapper is registered as a singleton in `Program.cs` with `AssertConfigurationIsValid()` at startup.

### Swagger

The API is documented via Swagger UI at `/swagger` (development-only). JWT bearer authentication is configured in Swagger — click the **Authorize** button and enter your JWT token to test protected endpoints.
When logged in web UI authenticated access of Swagger works out of the box.
