using BlazorWebAppMovies.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BlazorWebAppMovies.Tests.DatabaseTests;

public class DbContextProviderTests
{
    [Fact]
    public void DesignTimeDbContextFactory_CreatesSqliteContext()
    {
        var factory = new DesignTimeDbContextFactory();
        using var context = ((IDesignTimeDbContextFactory<BlazorWebAppMoviesContextSqlite>)factory).CreateDbContext([]);

        Assert.NotNull(context);
        Assert.IsType<BlazorWebAppMoviesContextSqlite>(context);
    }

    [Fact]
    public void DesignTimeDbContextFactory_CreatesSqlServerContext()
    {
        const string configFileName = "appsettings.json";
        var originalContent = File.Exists(configFileName) ? File.ReadAllText(configFileName) : null;

        try
        {
            // Override appsettings.json to use SqlServer provider
            File.WriteAllText(configFileName, /*lang=json*/ """
            {
              "DatabaseProvider": "SqlServer",
              "ConnectionStrings": {
                "BlazorWebAppMoviesContextSqlServer": "Server=(localdb)\\mssqllocaldb;Database=BlazorWebAppMoviesTest;Trusted_Connection=True;TrustServerCertificate=True;"
              }
            }
            """);

            var factory = new DesignTimeDbContextFactory();
            using var context = ((IDesignTimeDbContextFactory<BlazorWebAppMoviesContextSqlServer>)factory).CreateDbContext([]);

            Assert.NotNull(context);
            Assert.IsType<BlazorWebAppMoviesContextSqlServer>(context);
        }
        finally
        {
            // Restore original appsettings.json
            if (originalContent != null)
                File.WriteAllText(configFileName, originalContent);
            else if (File.Exists(configFileName))
                File.Delete(configFileName);
        }
    }

    [Fact]
    public void SqliteContext_IsAssignableToBaseContext()
    {
        var options = new DbContextOptionsBuilder<BlazorWebAppMoviesContextSqlite>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var context = new BlazorWebAppMoviesContextSqlite(options);
        Assert.IsAssignableFrom<BlazorWebAppMoviesContext>(context);
    }

    [Fact]
    public void SqlServerContext_IsAssignableToBaseContext()
    {
        var options = new DbContextOptionsBuilder<BlazorWebAppMoviesContextSqlServer>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=Test;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        using var context = new BlazorWebAppMoviesContextSqlServer(options);
        Assert.IsAssignableFrom<BlazorWebAppMoviesContext>(context);
    }

    [Fact]
    public async Task SqliteInMemoryContext_CanPerformCrud()
    {
        var options = new DbContextOptionsBuilder<BlazorWebAppMoviesContextSqlite>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        await using var context = new BlazorWebAppMoviesContextSqlite(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();

        var movie = new Models.Movie
        {
            Title = "SQLite Test",
            ReleaseDate = new DateOnly(2024, 1, 1),
            Genre = "Test",
            Price = 5m,
            Rating = "PG"
        };

        context.Movie.Add(movie);
        await context.SaveChangesAsync();

        var count = await context.Movie.CountAsync();
        Assert.Equal(1, count);
    }
}
