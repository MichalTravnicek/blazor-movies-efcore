using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using BlazorWebAppMovies.Models;

namespace BlazorWebAppMovies.Data
{
    public class BlazorWebAppMoviesContext(DbContextOptions<BlazorWebAppMoviesContext> options) : IdentityDbContext<User>(options)
    {
        public DbSet<Movie> Movie { get; set; } = default!;
    }
}
