using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BlazorWebAppMovies.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BlazorWebAppMoviesContextSqlite>,
                                          IDesignTimeDbContextFactory<BlazorWebAppMoviesContextSqlServer>
{
    private static T Create<T>(string[] args) where T : BlazorWebAppMoviesContext
    {
        var configPath = args.Length > 0 ? args[0] : "appsettings.json";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(configPath)
            .Build();

        var provider = DbContextProvider.Create(configuration);
        var optionsBuilder = new DbContextOptionsBuilder<BlazorWebAppMoviesContext>();
        provider.ConfigureDbContext(optionsBuilder);
        return (T)Activator.CreateInstance(typeof(T), optionsBuilder.Options)!;
    }

    BlazorWebAppMoviesContextSqlite IDesignTimeDbContextFactory<BlazorWebAppMoviesContextSqlite>.CreateDbContext(string[] args)
        => Create<BlazorWebAppMoviesContextSqlite>(args);

    BlazorWebAppMoviesContextSqlServer IDesignTimeDbContextFactory<BlazorWebAppMoviesContextSqlServer>.CreateDbContext(string[] args)
        => Create<BlazorWebAppMoviesContextSqlServer>(args);
}
