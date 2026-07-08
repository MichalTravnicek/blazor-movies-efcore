using Microsoft.EntityFrameworkCore;
using BlazorWebAppMovies.Models;
using Microsoft.AspNetCore.Identity;

namespace BlazorWebAppMovies.Data;

public class SeedData
{
    
    private static async Task SeedRolesAndAdmin(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<User>>();

        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        if (!await roleManager.RoleExistsAsync("User"))
        {
            await roleManager.CreateAsync(new IdentityRole("User"));
        }
        
        await CreateUserWithRole(userManager, "Admin","admin@example.com","Admin", "Admin123!");
        await CreateUserWithRole(userManager, "Michal","michal@michal.cz","User", "C0mpl3x!Pass");
    }

    private static async Task CreateUserWithRole(UserManager<User> userManager, string name, string userEmail, string role, string password)
    {
        var user = await userManager.FindByEmailAsync(userEmail);

        if (user == null)
        {
            user = new User
            {
                UserName = userEmail,
                Email = userEmail,
                Name = name
            };

            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }

    private static void SeedMovies(BlazorWebAppMoviesContext context)
    {
        if (context.Movie.Any())
        {
            return;
        }

        context.Movie.AddRange(
            new Movie
            {
                Title = "Mad Max",
                ReleaseDate = new DateOnly(1979, 4, 12),
                Genre = "Sci-fi (Cyberpunk)",
                Price = 2.51M,
                Rating = "R",
            },
            new Movie
            {
                Title = "The Road Warrior",
                ReleaseDate = new DateOnly(1981, 12, 24),
                Genre = "Sci-fi (Cyberpunk)",
                Price = 2.78M,
                Rating = "R",
            },
            new Movie
            {
                Title = "Mad Max: Beyond Thunderdome",
                ReleaseDate = new DateOnly(1985, 7, 10),
                Genre = "Sci-fi (Cyberpunk)",
                Price = 3.55M,
                Rating = "PG-13",
            },
            new Movie
            {
                Title = "Mad Max: Fury Road",
                ReleaseDate = new DateOnly(2015, 5, 15),
                Genre = "Sci-fi (Cyberpunk)",
                Price = 8.43M,
                Rating = "R",
            },
            new Movie
            {
                Title = "Furiosa: A Mad Max Saga",
                ReleaseDate = new DateOnly(2024, 5, 24),
                Genre = "Sci-fi (Cyberpunk)",
                Price = 13.49M,
                Rating = "R",
            });

        context.SaveChanges();
    }

    public static async Task Initialize(IDbContextFactory<BlazorWebAppMoviesContext> factory, IServiceProvider serviceProvider)
    {
        await using var context = factory.CreateDbContext();

        await SeedRolesAndAdmin(serviceProvider);
        SeedMovies(context);
    }
}
