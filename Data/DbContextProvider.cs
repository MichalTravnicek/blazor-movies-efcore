using Microsoft.EntityFrameworkCore;

namespace BlazorWebAppMovies.Data;

public abstract class DbContextProvider
{
    public abstract string ConnectionString { get; }
    public abstract void ConfigureDbContext(DbContextOptionsBuilder options);
    public abstract Type MigrationContextType { get; }
    public abstract void RegisterConcreteFactory(IServiceCollection services);

    public static DbContextProvider Create(IConfiguration configuration)
    {
        var provider = configuration["DatabaseProvider"] ?? "Sqlite";

        if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = configuration.GetConnectionString("BlazorWebAppMoviesContextSqlServer")
                ?? throw new InvalidOperationException("Connection string 'BlazorWebAppMoviesContextSqlServer' not found.");
            return new SqlServerDbContextProvider(connectionString);
        }

        var sqliteConnectionString = configuration.GetConnectionString("BlazorWebAppMoviesContextSqlite")
            ?? throw new InvalidOperationException("Connection string 'BlazorWebAppMoviesContextSqlite' not found.");
        return new SqliteDbContextProvider(sqliteConnectionString);
    }

    public BlazorWebAppMoviesContext CreateMigrationContext(IServiceProvider services)
    {
        var factoryType = typeof(IDbContextFactory<>).MakeGenericType(MigrationContextType);
        var factory = services.GetRequiredService(factoryType);
        var createMethod = factoryType.GetMethod("CreateDbContext")!;
        return (BlazorWebAppMoviesContext)createMethod.Invoke(factory, null)!;
    }
}

public class SqliteDbContextProvider(string connectionString) : DbContextProvider
{
    public override string ConnectionString { get; } = connectionString;
    public override Type MigrationContextType => typeof(BlazorWebAppMoviesContextSqlite);

    public override void ConfigureDbContext(DbContextOptionsBuilder options)
    {
        options.UseSqlite(ConnectionString);
    }

    public override void RegisterConcreteFactory(IServiceCollection services)
    {
        services.AddDbContextFactory<BlazorWebAppMoviesContextSqlite>(ConfigureDbContext);
    }
}

public class SqlServerDbContextProvider(string connectionString) : DbContextProvider
{
    public override string ConnectionString { get; } = connectionString;
    public override Type MigrationContextType => typeof(BlazorWebAppMoviesContextSqlServer);

    public override void ConfigureDbContext(DbContextOptionsBuilder options)
    {
        options.UseSqlServer(ConnectionString);
    }

    public override void RegisterConcreteFactory(IServiceCollection services)
    {
        services.AddDbContextFactory<BlazorWebAppMoviesContextSqlServer>(ConfigureDbContext);
    }
}