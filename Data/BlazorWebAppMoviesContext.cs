using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using BlazorWebAppMovies.Models;

namespace BlazorWebAppMovies.Data;

public class BlazorWebAppMoviesContext(DbContextOptions<BlazorWebAppMoviesContext> options) : IdentityDbContext<User>(options)
{
    public DbSet<Movie> Movie { get; set; } = default!;

    public DbSet<MovieRating> MovieRating { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Movie>()
            .HasIndex(m => m.Title)
            .IsUnique();

        builder.Entity<Movie>()
            .HasOne(m => m.MovieRating)
            .WithMany(r => r.Movies)
            .HasForeignKey(m => m.MovieRatingId)
            .OnDelete(DeleteBehavior.Restrict);


    }
}
