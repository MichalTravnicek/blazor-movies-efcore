# Backend Architecture Guide

> BlazorWebAppMovies — .NET 9 Backend
> Document version: 2026-07-08

---

## Overview

The backend is a .NET 9 ASP.NET Core application using Blazor Server Interactive with JWT Bearer authentication, Entity Framework Core (dual provider: SQLite + SQL Server), and ASP.NET Core Identity. It exposes RESTful API controllers consumed by the Classic UI and external clients, while the Blazor UI accesses the database directly via `DbContextFactory` and `UserManager`.

---

## Startup Pipeline (`Program.cs`)

```
Program.cs
│
├── 1. Service Registration
│   ├── DbContextProvider ──→ DbContextFactory (SQLite / SQL Server)
│   ├── Identity (UserManager, RoleManager, SignInManager)
│   ├── JWT Bearer Authentication (cookie + header)
│   ├── Authorization (role-based)
│   ├── Razor Pages + Controllers + Swagger
│   ├── AutoMapper (singleton, validated at startup)
│   └── Blazor Components (Interactive Server)
│
├── 2. Middleware Pipeline
│   ├── Exception Handling (dev vs prod)
│   ├── Swagger (dev only)
│   ├── HTTPS Redirection
│   ├── Authentication
│   ├── Authorization
│   ├── Antiforgery
│   ├── Static Files
│   ├── Request Logging (API + Classic only)
│   └── Endpoints (Razor Pages, Controllers, Blazor)
│
└── 3. Startup (migrations + seed data)
    ├── Check for pending model changes
    ├── Apply migrations (Database.Migrate)
    └── SeedData.Initialize (dev only)
```

### Key Configuration

| Setting | File | Purpose |
|---------|------|---------|
| `DatabaseProvider` | `appsettings.json` | `Sqlite` or `SqlServer` |
| `Jwt:Key` | `appsettings.json` | Symmetric signing key (min 32 bytes) |
| `Jwt:Issuer` | `appsettings.json` | Token issuer |
| `Jwt:Audience` | `appsettings.json` | Token audience |

---

## Layered Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Presentation Layer                       │
│                                                                 │
│  ┌──────────────────────┐  ┌────────────────────────────────┐   │
│  │   Blazor Components  │  │   API Controllers              │   │
│  │   (.razor)           │  │   (Movies, Auth, Admin)        │   │
│  └──────────┬───────────┘  └───────────────┬────────────────┘   │
│             │                              │                    │
└─────────────┼──────────────────────────────┼────────────────────┘
              │                              │
┌─────────────┼──────────────────────────────┼─────────────────────┐
│             │           Service Layer      │                     │
│  ┌──────────┴───────────┐  ┌───────────────┴────────────────┐    │
│  │   UserManager<T>     │  │   AutoMapper                   │    │
│  │   SignInManager<T>   │  │   (Movie → MovieDto, etc.)     │    │
│  │   RoleManager<T>     │  └────────────────────────────────┘    │
│  └──────────────────────┘                                        │
└──────────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────┼─────────────────────────────────────┐
│              Data Layer     │                                     │
│  ┌──────────────────────────┴───────────────────────────────┐     │
│  │                    DbContext                             │     │
│  │                                                          │     │
│  │  ┌─ BlazorWebAppMoviesContext (base)                     │     │
│  │  │  ├── DbSet<Movie>                                     │     │
│  │  │  └── Identity tables (via IdentityDbContext<User>)    │     │
│  │  ├── BlazorWebAppMoviesContextSqlite (SQLite)            │     │
│  │  └── BlazorWebAppMoviesContextSqlServer (SQL Server)     │     │
│  └────────────────────────────────────── ───────────────────┘     │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────┐     │
│  │  DbContextProviderFactory                                │     │
│  │  ├── IDbContextFactory<BlazorWebAppMoviesContext>        │     │
│  │  ├── IDbContextFactory<BlazorWebAppMoviesContextSqlite>  │     │
│  │  └── IDbContextFactory<BlazorWebAppMoviesContextSqlServer│     │
│  └──────────────────────────────────────────────────────────┘     │
└───────────────────────────────────────────────────────────────────┘
```

---

## Authentication System

### JWT Bearer Flow

```
Client                    Server
  │                         │
  │  POST /api/auth/login   │
  │  { email, password }    │
  │────────────────────────►│
  │                         ├── FindByEmailAsync
  │                         ├── CheckPasswordSignInAsync
  │                         ├── GenerateJwtToken (24h expiry)
  │                         │   └── claims: NameIdentifier, Email, Name, role(s)
  │                         └── Set-Cookie: auth_token=<jwt>; HttpOnly; Secure; SameSite=Strict
  │◄────────────────────────│
  │                         │
  │  GET /api/movies        │
  │  Cookie: auth_token=... │
  │────────────────────────►│
  │                         ├── OnMessageReceived → reads cookie → sets context.Token
  │                         ├── JWT validated (issuer, audience, lifetime, signing key)
  │                         ├── ClaimsPrincipal populated
  │                         └── Controller action executes
  │◄────────────────────────│
```

### Token Contents

| Claim | Type | Example |
|-------|------|---------|
| `nameid` (NameIdentifier) | string | `e60fa130-...` (user ID) |
| `email` | string | `admin@example.com` |
| `name` | string | `Admin` |
| `role` | string[] | `["Admin"]` |

### Key Configuration

In `Program.cs`:
- `MapInboundClaims = false` — preserves custom claim types
- `RoleClaimType = "role"` — JWT uses lowercase `role` claim
- `OnMessageReceived` — reads JWT from `auth_token` cookie for Blazor SignalR circuit
- Token expiry: 24 hours

---

## API Controllers

### MoviesController

| Aspect | Detail |
|--------|--------|
| **Route** | `/api/movies` |
| **Auth** | `[Authorize]` on class, `[AllowAnonymous]` on GET |
| **DI** | `IDbContextFactory<BlazorWebAppMoviesContext>`, `IMapper` |
| **DTOs** | `MovieDto` (output), `CreateMovieDto` (input), `UpdateMovieDto` (input) |
| **Mapping** | AutoMapper `MovieProfile` (3 mappings) |
| **Duplicate check** | Case-insensitive title check before create/update |

### AuthController

| Aspect | Detail |
|--------|--------|
| **Route** | `/api/auth` |
| **Auth** | `[Authorize]` on logout only |
| **DI** | `UserManager<User>`, `SignInManager<User>`, `IConfiguration` |
| **Endpoints** | `login` (POST, public), `register` (POST, public), `logout` (POST, authorized) |
| **Cookie** | JWT stored in `auth_token` cookie (HttpOnly, Secure, SameSite=Strict) |

### AdminController

| Aspect | Detail |
|--------|--------|
| **Route** | `/api/admin` |
| **Auth** | `[Authorize(Roles = "Admin")]` |
| **DI** | `UserManager<User>` (primary constructor) |
| **Endpoints** | `users` (GET, POST), `users/{id}` (PUT, DELETE), `users/{id}/password` (PUT) |
| **Error helpers** | `NotFoundResponse()`, `ErrorResponse(IdentityResult)` |

---

## Data Layer

### DbContext Hierarchy

```
BlazorWebAppMoviesContext (abstract base)
  ├── DbSet<Movie>
  └── IdentityDbContext<User> (all Identity tables)
      │
      ├── BlazorWebAppMoviesContextSqlite
      │   └── OnModelCreating: unique index on Movie.Title
      │
      └── BlazorWebAppMoviesContextSqlServer
          └── Same schema, different provider
```

### DbContextProvider

The `DbContextProvider` abstract class selects the correct provider based on the `DatabaseProvider` config key:

| Provider | Config Value | DbContext | Migration Folder |
|----------|-------------|-----------|------------------|
| SQLite | `Sqlite` | `BlazorWebAppMoviesContextSqlite` | `Migrations/Sqlite/` |
| SQL Server | `SqlServer` | `BlazorWebAppMoviesContextSqlServer` | `Migrations/SqlServer/` |

The provider is registered in `Program.cs`:
```csharp
var dbProvider = DbContextProvider.Create(builder.Configuration);
builder.Services.AddDbContextFactory<BlazorWebAppMoviesContext>(dbProvider.ConfigureDbContext);
dbProvider.RegisterConcreteFactory(builder.Services);
```

### Entity Models

#### Movie

| Property | Type | Validation |
|----------|------|------------|
| `Id` | `int` | Auto-generated |
| `Title` | `string?` | Required, 3-60 chars, not whitespace-only |
| `ReleaseDate` | `DateOnly` | Required |
| `Genre` | `string?` | Required, max 30, starts with uppercase |
| `Price` | `decimal` | 0-100, currency |
| `Rating` | `string?` | Required, regex: G/PG/PG-13/R/NC-17 |

#### User

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `string` | Inherited from `IdentityUser` (GUID) |
| `Name` | `string?` | Custom display name (not in default Identity) |
| `UserName` | `string?` | Inherited (set to email) |
| `Email` | `string?` | Inherited |

### Seed Data

On first run in Development mode, `SeedData.Initialize`:

1. Creates **Admin** and **User** roles (if not exist)
2. Creates admin user `admin@example.com` / `Admin123!` with Admin role (if not exist)
3. Seeds 5 Mad Max movies (if none exist)

---

## AutoMapper

### Registration

Registered as a singleton with `AssertConfigurationIsValid()` at startup:
```csharp
builder.Services.AddSingleton(sp =>
{
    var config = new MapperConfiguration(cfg => cfg.AddProfile<MovieProfile>());
    config.AssertConfigurationIsValid();
    return config.CreateMapper();
});
```

### MovieProfile Mappings

| Source | Destination | Notes |
|--------|-------------|-------|
| `Movie` | `MovieDto` | All properties, Id included |
| `CreateMovieDto` | `Movie` | Id ignored (DB-generated) |
| `UpdateMovieDto` | `Movie` | Id ignored (preserved from existing entity) |

### DTOs

| DTO | Direction | Purpose |
|-----|-----------|---------|
| `MovieDto` | Output | Full movie data (Id, Title, ReleaseDate, Genre, Price, Rating) |
| `CreateMovieDto` | Input | Create payload, validated |
| `UpdateMovieDto` | Input | Update payload, validated |

All input DTOs carry the same validation attributes as the `Movie` entity.

---

## Request Logging Middleware

Located in `Program.cs` after `UseAuthentication()`, so the `User` principal is populated:

```csharp
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (path.StartsWithSegments("/api") || path.StartsWithSegments("/classic"))
    {
        Console.WriteLine($"[REQ] {method} {path} - {auth}, user: {user}");
        var stopwatch = Stopwatch.StartNew();
        await next();
        Console.WriteLine($"[RES] {method} {path} - {statusCode} {elapsed:F0}ms");
    }
    else
    {
        await next();
    }
});
```

Only logs requests to `/api/*` and `/classic/*` paths. Blazor SignalR requests are excluded.

---

## Dependency Injection Overview

| Service | Lifetime | Registered In | Used By |
|---------|----------|---------------|---------|
| `IDbContextFactory<T>` | Singleton | `Program.cs` | Controllers, Blazor Pages |
| `UserManager<User>` | Scoped | Identity | AuthController, AdminController, Blazor Pages, Classic Pages |
| `SignInManager<User>` | Scoped | Identity | AuthController |
| `RoleManager<IdentityRole>` | Scoped | Identity | SeedData |
| `IMapper` | Singleton | `Program.cs` (manual) | MoviesController |
| `IConfiguration` | Singleton | Built-in | AuthController, DbContextProvider |

---

## Swagger

Available at `/swagger` in Development mode only.

| Feature | Implementation |
|---------|---------------|
| **Security definition** | Bearer token (JWT) |
| **Examples** | `SchemaFilter<SwaggerExampleFilter>` — realistic movie examples |
| **Login instructions** | `OperationFilter<SwaggerLoginDescriptionFilter>` — explains how to get JWT |

---

## Middleware Pipeline Order

```
1. ExceptionHandler / DeveloperExceptionPage
2. HSTS (production only)
3. Swagger + SwaggerUI (development only)
4. HTTPS Redirection
5. Static Files
6. Authentication
7. Authorization
8. Antiforgery
9. Request Logging (API + Classic only)
10. Endpoints:
    ├── MapStaticAssets
    ├── MapRazorPages
    ├── MapControllers
    └── MapRazorComponents<App>
```

---

## Project Structure (Backend)

```
├── Program.cs                    # Startup, DI, middleware, pipeline
├── BlazorWebAppMovies.csproj     # Project file
├── appsettings.json              # Configuration (JWT, DB, connection strings)
│
├── Controllers/
│   ├── AuthController.cs         # Login, register, logout
│   ├── MoviesController.cs       # Movie CRUD (with AutoMapper)
│   └── AdminController.cs        # User CRUD (admin only)
│
├── Data/
│   ├── BlazorWebAppMoviesContext.cs       # Base DbContext (abstract)
│   ├── BlazorWebAppMoviesContextSqlite.cs  # SQLite concrete
│   ├── BlazorWebAppMoviesContextSqlServer.cs # SQL Server concrete
│   ├── DbContextProvider.cs               # Provider factory + SQLite/SQL Server impls
│   ├── DesignTimeDbContextFactory.cs       # EF Core CLI tooling support
│   └── SeedData.cs                        # Initial data seeding
│
├── Models/
│   ├── Movie.cs                  # Movie entity with validation
│   ├── User.cs                   # IdentityUser with Name property
│   └── Dtos/
│       ├── MovieDto.cs           # Output DTO
│       ├── CreateMovieDto.cs     # Create input DTO (validated)
│       └── UpdateMovieDto.cs     # Update input DTO (validated)
│
├── Models/Mapping/
│   └── MovieProfile.cs           # AutoMapper profile (3 mappings)
│
├── Migrations/
│   ├── Sqlite/                   # SQLite migrations
│   └── SqlServer/                # SQL Server migrations
│
├── SwaggerExampleFilter.cs       # Realistic Swagger examples
├── SwaggerLoginDescriptionFilter.cs # Swagger login instructions
└── Components/                   # Blazor components (UI layer)
```