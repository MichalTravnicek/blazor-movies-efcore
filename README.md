# Blazor Web App Movies tutorial sample app

This sample app is the completed app for the Blazor Web App Movies tutorial:

[Build a Blazor movie database app (Overview)](https://learn.microsoft.com/aspnet/core/blazor/tutorials/movie-database-app/)

The app supports both **SQLite** (default) and **SQL Server** databases.  
Switch between them by changing the `DatabaseProvider` setting in `appsettings.json`.

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- For SQL Server: [SQL Server LocalDB](https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb) (included with Visual Studio)
- `dotnet-ef` tool (restored automatically via `.config/dotnet-tools.json`)

## Configuration

The database provider is controlled by the `DatabaseProvider` key in `appsettings.json`:
```
"DatabaseProvider": "Sqlite" // Uses SQLite (default) "DatabaseProvider": "SqlServer" // Uses SQL Server LocalDB
```

The app includes two connection strings:

| Connection String | Purpose |
|---|---|
| `BlazorWebAppMoviesContext` | SQLite: `Data Source=BlazorWebAppMovies.db` |
| `BlazorWebAppMoviesContextSqlServer` | SQL Server LocalDB |

---

## Restore the EF Core tool

Before running any migration commands, restore the `dotnet-ef` tool:
```
dotnet tool restore
```

## Creating and applying migrations

Migrations are stored in separate subfolders based on the provider.

### SQLite migrations

1. Ensure `"DatabaseProvider": "Sqlite"` in `appsettings.json`
2. Generate:
   ```
   dotnet ef migrations add InitialCreate --output-dir Migrations\Sqlite
   ```
3. Apply:
   ```
   dotnet ef database update
   ```

### SQL Server migrations

1. Set `"DatabaseProvider": "SqlServer"` in `appsettings.json`
2. Generate:
   ```
   dotnet ef migrations add InitialCreate --output-dir Migrations\SqlServer
   ```
3. Apply:
   ```
   dotnet ef database update
   ```
4. To switch back to SQLite, set `"DatabaseProvider": "Sqlite"` in `appsettings.json`

## Running the app

### Using SQLite (default)

1. Ensure `"DatabaseProvider": "Sqlite"` in `appsettings.json`
2. Generate and apply SQLite migrations (see above)
3. Run the app:
   ```
   dotnet run
   ```

### Using SQL Server

1. Set `"DatabaseProvider": "SqlServer"` in `appsettings.json`
2. Generate and apply SQL Server migrations (see above)
3. Run the app:
   ```
   dotnet run
   ```

The seed data (`SeedData.Initialize`) populates the database with sample movies on first run.

## Default URLs

- HTTPS: `https://localhost:7073`
- HTTP: `http://localhost:5261`

You can change these in `Properties/launchSettings.json`.

## Clean up

### SQLite

Delete the `BlazorWebAppMovies.db` file from the project folder.

### SQL Server

1. Open **SQL Server Object Explorer** (View > SQL Server Object Explorer)
2. Navigate to `SQL Server` > `(localdb)\MSSQLLocalDB` > `Databases`
3. Right-click `BlazorWebAppMovies` and select **Delete**
4. Check **Close existing connections** and select **OK**
