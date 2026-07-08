using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BlazorWebAppMovies.Models;

namespace BlazorWebAppMovies.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController(UserManager<User> userManager) : ControllerBase
{
    public record CreateUserRequest(string Name, string Email, string Password, string Role);
    public record UpdateUserRequest(string Name, string Email, string? Role);
    public record ChangePasswordRequest(string NewPassword);

    private ActionResult NotFoundResponse() =>
        NotFound(new { Message = "User not found." });

    private ActionResult ErrorResponse(IdentityResult result) =>
        BadRequest(new { Message = string.Join("; ", result.Errors.Select(e => e.Description)) });
    
    /// <summary>
    /// GET /api/admin/users — list all users with roles
    /// </summary>
    [HttpGet("users")]
    public async Task<ActionResult<List<UserDto>>> GetUsers()
    {
        var users = await userManager.Users.ToListAsync();
        var result = new List<UserDto>();
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            result.Add(new UserDto
            {
                Id = user.Id,
                Name = user.Name ?? "",
                Email = user.Email ?? "",
                Roles = roles.ToList()
            });
        }
        return Ok(result.OrderBy(u => u.Name).ToList());
    }

    /// <summary>
    /// POST /api/admin/users — create a new user
    /// </summary>
    [HttpPost("users")]
    public async Task<ActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (await userManager.FindByEmailAsync(request.Email) != null)
            return Conflict(new { Message = $"User with email '{request.Email}' already exists." });

        var user = new User
        {
            UserName = request.Email,
            Email = request.Email,
            Name = request.Name
        };
        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
            return ErrorResponse(createResult);

        await userManager.AddToRoleAsync(user, request.Role == "Admin" ? "Admin" : "User");
        return Ok(new { Message = $"User '{request.Name}' created successfully." });
    }

    /// <summary>
    /// PUT /api/admin/users/{id} — update a user's name, email, and role
    /// </summary>
    [HttpPut("users/{id}")]
    public async Task<ActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null) return NotFoundResponse();

        if (!string.Equals(request.Email, user.Email, StringComparison.OrdinalIgnoreCase)
            && await userManager.FindByEmailAsync(request.Email) != null)
            return Conflict(new { Message = $"User with email '{request.Email}' already exists." });

        user.Name = request.Name;
        user.Email = request.Email;
        user.UserName = request.Email;

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return ErrorResponse(updateResult);

        if (!string.IsNullOrEmpty(request.Role))
        {
            var currentRoles = await userManager.GetRolesAsync(user);
            await userManager.RemoveFromRolesAsync(user, currentRoles);
            await userManager.AddToRoleAsync(user, request.Role);
        }

        return Ok(new { Message = "User updated successfully." });
    }

    /// <summary>
    /// PUT /api/admin/users/{id}/password — change a user's password
    /// </summary>
    [HttpPut("users/{id}/password")]
    public async Task<ActionResult> ChangePassword(string id, [FromBody] ChangePasswordRequest request)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null) return NotFoundResponse();

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);
        if (!result.Succeeded)
            return ErrorResponse(result);

        return Ok(new { Message = "Password changed successfully." });
    }

    /// <summary>
    /// DELETE /api/admin/users/{id} — delete a user
    /// </summary>
    [HttpDelete("users/{id}")]
    public async Task<ActionResult> DeleteUser(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null) return NotFoundResponse();

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return ErrorResponse(result);

        return Ok(new { Message = "User deleted successfully." });
    }
}

public class UserDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public List<string> Roles { get; set; } = new();
    public bool IsAdmin => Roles.Contains("Admin");
}
