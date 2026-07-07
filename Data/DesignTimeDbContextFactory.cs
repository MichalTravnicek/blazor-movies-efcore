using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BlazorWebAppMovies.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BlazorWebAppMoviesContextSqlite>,
                                          IDesignTimeDbContextFactory<BlazorWebAppMoviesContextSqlServer>,
                                          IDesignTimeDbContextFactory<BlazorWebAppMoviesContext>
{
    BlazorWebAppMoviesContextSqlite IDesignTimeDbContextFactory<BlazorWebAppMoviesContextSqlite>.CreateDbContext(string[] args)
    {
        var configPath = args.Length > 0 ? args[0] : "appsettings.json";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(configPath)
            .Build();

        var connectionString = configuration.GetConnectionString("BlazorWebAppMoviesContext")
            ?? throw new InvalidOperationException("SQLite connection string not found.");
        var optionsBuilder = new DbContextOptionsBuilder<BlazorWebAppMoviesContext>();
        optionsBuilder.UseSqlite(connectionString);
        return new BlazorWebAppMoviesContextSqlite(optionsBuilder.Options);
    }

    BlazorWebAppMoviesContextSqlServer IDesignTimeDbContextFactory<BlazorWebAppMoviesContextSqlServer>.CreateDbContext(string[] args)
    {
        var configPath = args.Length > 0 ? args[0] : "appsettings.json";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(configPath)
            .Build();

        var connectionString = configuration.GetConnectionString("BlazorWebAppMoviesContextSqlServer")
            ?? throw new InvalidOperationException("SQL Server connection string not found.");
        var optionsBuilder = new DbContextOptionsBuilder<BlazorWebAppMoviesContext>();
        optionsBuilder.UseSqlServer(connectionString);
        return new BlazorWebAppMoviesContextSqlServer(optionsBuilder.Options);
    }

    // Keep the original factory for backward compatibility
    BlazorWebAppMoviesContext IDesignTimeDbContextFactory<BlazorWebAppMoviesContext>.CreateDbContext(string[] args)
    {
        return ((IDesignTimeDbContextFactory<BlazorWebAppMoviesContextSqlite>)this).CreateDbContext(args);
    }
}
