using Microsoft.EntityFrameworkCore;

namespace BlazorWebAppMovies.Data;

public class BlazorWebAppMoviesContextSqlServer(DbContextOptions<BlazorWebAppMoviesContext> options)
    : BlazorWebAppMoviesContext(options)
{
}
