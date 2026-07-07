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
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var configPath = Path.Combine(tempDir, "appsettings.json");

        try
        {
            File.WriteAllText(configPath, /*lang=json*/ """
            {
              "DatabaseProvider": "SqlServer",
              "ConnectionStrings": {
                "BlazorWebAppMoviesContextSqlServer": "Server=(localdb)\\mssqllocaldb;Database=BlazorWebAppMoviesTest;Trusted_Connection=True;TrustServerCertificate=True;"
              }
            }
            """);

            var factory = new DesignTimeDbContextFactory();
            using var context = ((IDesignTimeDbContextFactory<BlazorWebAppMoviesContextSqlServer>)factory).CreateDbContext([configPath]);

            Assert.NotNull(context);
            Assert.IsType<BlazorWebAppMoviesContextSqlServer>(context);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void SqliteContext_IsAssignableToBaseContext()
    {
        var options = new DbContextOptionsBuilder<BlazorWebAppMoviesContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var context = new BlazorWebAppMoviesContextSqlite(options);
        Assert.IsAssignableFrom<BlazorWebAppMoviesContext>(context);
    }

    [Fact]
    public void SqlServerContext_IsAssignableToBaseContext()
    {
        var options = new DbContextOptionsBuilder<BlazorWebAppMoviesContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=Test;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        using var context = new BlazorWebAppMoviesContextSqlServer(options);
        Assert.IsAssignableFrom<BlazorWebAppMoviesContext>(context);
    }

    [Fact]
    public async Task SqliteInMemoryContext_CanPerformCrud()
    {
        var options = new DbContextOptionsBuilder<BlazorWebAppMoviesContext>()
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
