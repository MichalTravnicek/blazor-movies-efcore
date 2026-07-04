using Microsoft.EntityFrameworkCore;

namespace BlazorWebAppMovies.Data
{
    public class BlazorWebAppMoviesContextSqlite(DbContextOptions<BlazorWebAppMoviesContext> options)
        : BlazorWebAppMoviesContext(options)
    {
    }
}