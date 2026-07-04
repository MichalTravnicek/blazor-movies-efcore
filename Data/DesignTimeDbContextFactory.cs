using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BlazorWebAppMovies.Data
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BlazorWebAppMoviesContext>
    {
        public BlazorWebAppMoviesContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var provider = configuration["DatabaseProvider"] ?? "Sqlite";
            var optionsBuilder = new DbContextOptionsBuilder<BlazorWebAppMoviesContext>();

            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                var connectionString = configuration.GetConnectionString("BlazorWebAppMoviesContextSqlServer")
                    ?? throw new InvalidOperationException("SQL Server connection string not found.");
                optionsBuilder.UseSqlServer(connectionString);
                return new BlazorWebAppMoviesContextSqlServer(optionsBuilder.Options);
            }
            else
            {
                var connectionString = configuration.GetConnectionString("BlazorWebAppMoviesContext")
                    ?? throw new InvalidOperationException("SQLite connection string not found.");
                optionsBuilder.UseSqlite(connectionString);
                return new BlazorWebAppMoviesContextSqlite(optionsBuilder.Options);
            }
        }
    }
}