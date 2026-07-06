using Microsoft.EntityFrameworkCore;

namespace BlazorWebAppMovies.Data
{
    public class BlazorWebAppMoviesContextSqlServer(DbContextOptions<BlazorWebAppMoviesContextSqlServer> options)
        : BlazorWebAppMoviesContext(options)
    {
    }
}
