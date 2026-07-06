using Microsoft.EntityFrameworkCore;

namespace BlazorWebAppMovies.Data
{
    public class BlazorWebAppMoviesContextSqlite(DbContextOptions<BlazorWebAppMoviesContextSqlite> options)
        : BlazorWebAppMoviesContext(options)
    {
    }
}
