using Microsoft.AspNetCore.Identity;

namespace BlazorWebAppMovies.Models;

public class User : IdentityUser
{
    public string? Name { get; set; }
}
