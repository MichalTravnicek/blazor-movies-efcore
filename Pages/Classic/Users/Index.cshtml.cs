using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BlazorWebAppMovies.Models;
using System.Text.Json;

namespace BlazorWebAppMovies.Pages.Classic.Users;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly UserManager<User> _userManager;

    public IndexModel(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public string? CurrentUserId { get; set; }
    public string? UsersJson { get; set; }

    public async Task OnGet()
    {
        var user = await _userManager.GetUserAsync(User);
        CurrentUserId = user?.Id;

        var users = await _userManager.Users.ToListAsync();
        var userDtos = new List<Dictionary<string, object>>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            userDtos.Add(new Dictionary<string, object>
            {
                ["id"] = u.Id,
                ["name"] = u.Name ?? "",
                ["email"] = u.Email ?? "",
                ["roles"] = roles.ToList(),
                ["isAdmin"] = roles.Contains("Admin")
            });
        }

        UsersJson = JsonSerializer.Serialize(userDtos.OrderBy(u => (string)u["name"]).ToList());
    }
}
