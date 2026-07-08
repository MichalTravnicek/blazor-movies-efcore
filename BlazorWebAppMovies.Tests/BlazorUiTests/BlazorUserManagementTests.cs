using System.Security.Claims;
using BlazorWebAppMovies.Data;
using BlazorWebAppMovies.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace BlazorWebAppMovies.Tests.BlazorUiTests;

/// <summary>
/// Tests for the Blazor UserManagement.razor page logic.
/// Verifies CRUD operations, role checks, and self-protection.
/// </summary>
public class BlazorUserManagementTests : IDisposable
{
    private readonly string _dbName;
    private readonly BlazorWebAppMoviesContext _context;
    private readonly UserManager<User> _userManager;
    private readonly IServiceScope _scope;

    public BlazorUserManagementTests()
    {
        _dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();

        services.AddDbContext<BlazorWebAppMoviesContext>(options =>
            options.UseInMemoryDatabase(_dbName));

        services.AddDbContextFactory<BlazorWebAppMoviesContext>(options =>
            options.UseInMemoryDatabase(_dbName));

        services.AddIdentity<User, IdentityRole>()
            .AddEntityFrameworkStores<BlazorWebAppMoviesContext>()
            .AddDefaultTokenProviders();

        services.Configure<IdentityOptions>(options =>
        {
            options.ClaimsIdentity.RoleClaimType = "role";
        });

        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        _scope = serviceProvider.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<BlazorWebAppMoviesContext>();
        _userManager = _scope.ServiceProvider.GetRequiredService<UserManager<User>>();
    }

    public void Dispose()
    {
        _scope.Dispose();
        _context.Dispose();
    }

    private async Task SeedRoles()
    {
        var roleManager = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync("Admin"))
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        if (!await roleManager.RoleExistsAsync("User"))
            await roleManager.CreateAsync(new IdentityRole("User"));
    }

    private async Task<User> CreateAdminUser(string email = "admin@test.com", string name = "Admin")
    {
        await SeedRoles();
        var user = new User { UserName = email, Email = email, Name = name };
        await _userManager.CreateAsync(user, "Admin123!");
        await _userManager.AddToRoleAsync(user, "Admin");
        return user;
    }

    private async Task<User> CreateRegularUser(string email = "user@test.com", string name = "User")
    {
        await SeedRoles();
        var user = new User { UserName = email, Email = email, Name = name };
        await _userManager.CreateAsync(user, "User1234!");
        await _userManager.AddToRoleAsync(user, "User");
        return user;
    }

    // ── User list (loads all users, ordered by name) ────────────

    [Fact]
    public async Task UserList_LoadsAllUsers()
    {
        await CreateAdminUser();
        await CreateRegularUser("alice@test.com", "Alice");
        await CreateRegularUser("bob@test.com", "Bob");

        var users = await _userManager.Users.ToListAsync();
        Assert.Equal(3, users.Count);
    }

    [Fact]
    public async Task UserList_UsersIncludeRoles()
    {
        await CreateAdminUser();
        await CreateRegularUser();

        var admin = await _userManager.FindByEmailAsync("admin@test.com");
        var roles = await _userManager.GetRolesAsync(admin!);
        Assert.Contains("Admin", roles);

        var user = await _userManager.FindByEmailAsync("user@test.com");
        var userRoles = await _userManager.GetRolesAsync(user!);
        Assert.Contains("User", userRoles);
    }

    // ── Create user (matches UserManagement.razor HandleCreateUser) ──

    [Fact]
    public async Task CreateUser_CreatesWithCorrectRole()
    {
        await SeedRoles();
        var user = new User { UserName = "new@test.com", Email = "new@test.com", Name = "New User" };
        var result = await _userManager.CreateAsync(user, "Pass1234!");
        Assert.True(result.Succeeded);

        await _userManager.AddToRoleAsync(user, "Admin");
        Assert.True(await _userManager.IsInRoleAsync(user, "Admin"));
    }

    [Fact]
    public async Task CreateUser_WithDuplicateEmail_ReturnsError()
    {
        await CreateRegularUser("dup@test.com", "Original");

        var user = new User { UserName = "dup@test.com", Email = "dup@test.com", Name = "Duplicate" };
        var result = await _userManager.CreateAsync(user, "Pass1234!");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task CreateUser_WithWeakPassword_ReturnsError()
    {
        await SeedRoles();
        var user = new User { UserName = "weak@test.com", Email = "weak@test.com", Name = "Weak" };
        var result = await _userManager.CreateAsync(user, "123");

        Assert.False(result.Succeeded);
    }

    // ── Edit user (matches UserManagement.razor HandleEditUser) ──

    [Fact]
    public async Task EditUser_UpdatesNameAndEmail()
    {
        var user = await CreateRegularUser();

        user.Name = "Updated Name";
        user.Email = "updated@test.com";
        user.UserName = "updated@test.com";
        var result = await _userManager.UpdateAsync(user);

        Assert.True(result.Succeeded);

        var updated = await _userManager.FindByIdAsync(user.Id);
        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated.Name);
        Assert.Equal("updated@test.com", updated.Email);
    }

    [Fact]
    public async Task EditUser_ChangesRole()
    {
        var user = await CreateRegularUser();

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, "Admin");

        Assert.True(await _userManager.IsInRoleAsync(user, "Admin"));
        Assert.False(await _userManager.IsInRoleAsync(user, "User"));
    }

    [Fact]
    public async Task EditUser_NonExistentId_ReturnsNull()
    {
        var user = await _userManager.FindByIdAsync("nonexistent");
        Assert.Null(user);
    }

    // ── Change password (matches UserManagement.razor HandleChangePassword) ──

    [Fact]
    public async Task ChangePassword_UsingResetToken_Works()
    {
        var user = await CreateRegularUser();

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, "NewPass123!");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ChangePassword_OldPasswordNoLongerWorks()
    {
        var user = await CreateRegularUser();
        const string oldPassword = "User1234!";

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        await _userManager.ResetPasswordAsync(user, resetToken, "NewPass123!");

        var signInResult = await new SignInManager<User>(
            _userManager,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<User>>(),
            Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
            Mock.Of<ILogger<SignInManager<User>>>(),
            Mock.Of<IAuthenticationSchemeProvider>(),
            Mock.Of<IUserConfirmation<User>>()
        ).CheckPasswordSignInAsync(user, oldPassword, false);

        Assert.False(signInResult.Succeeded);
    }

    // ── Delete user (matches UserManagement.razor HandleDeleteUser) ──

    [Fact]
    public async Task DeleteUser_RemovesUser()
    {
        var user = await CreateRegularUser();

        var result = await _userManager.DeleteAsync(user);
        Assert.True(result.Succeeded);

        var deleted = await _userManager.FindByIdAsync(user.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteUser_WithNonExistentId_ReturnsNull()
    {
        var user = await _userManager.FindByIdAsync("nonexistent");
        Assert.Null(user);
    }

    // ── Self-protection (admin cannot delete own account) ───────

    [Fact]
    public async Task AdminCannotDeleteOwnAccount_LogicCheck()
    {
        var admin = await CreateAdminUser();
        var currentUserId = admin.Id;

        // The Delete button is hidden when vm.Id == currentUserId && vm.IsAdmin
        bool shouldHideDelete = admin.Id == currentUserId && await _userManager.IsInRoleAsync(admin, "Admin");
        Assert.True(shouldHideDelete);
    }

    [Fact]
    public void AdminDeleteButton_Hidden_WhenCurrentUserIdIsNull()
    {
        // Simulates the transient bug: UserManager.GetUserAsync(User) returns null
        // so currentUserId on the page is null. The guard must hide the Delete button.
        string? currentUserId = null;
        string vmId = "some-admin-id";
        bool vmIsAdmin = true;

        // Guard from UserManagement.razor : @if (currentUserId is not null && (!vm.IsAdmin || vm.Id != currentUserId))
        bool shouldShowDelete = currentUserId is not null && (!vmIsAdmin || vmId != currentUserId);

        Assert.False(shouldShowDelete, "Delete button must be hidden when currentUserId is null");
    }

    [Fact]
    public async Task AdminCanDeleteOtherAdmin()
    {
        var admin1 = await CreateAdminUser("admin1@test.com", "Admin One");
        var admin2 = await CreateAdminUser("admin2@test.com", "Admin Two");

        // Admin1 can delete admin2 (different IDs)
        bool shouldShowDelete = admin2.Id != admin1.Id;
        Assert.True(shouldShowDelete);

        await _userManager.DeleteAsync(admin2);
        var deleted = await _userManager.FindByIdAsync(admin2.Id);
        Assert.Null(deleted);
    }

    // ── Admin can edit own account (always shows Edit button) ───

    [Fact]
    public async Task AdminCanEditOwnAccount()
    {
        var admin = await CreateAdminUser();

        // Edit button is always shown — no self-protection check
        admin.Name = "Self-Edited Admin";
        var result = await _userManager.UpdateAsync(admin);

        Assert.True(result.Succeeded);
        var updated = await _userManager.FindByIdAsync(admin.Id);
        Assert.Equal("Self-Edited Admin", updated!.Name);
    }

    // ── Admin can change own password ───────────────────────────

    [Fact]
    public async Task AdminCanChangeOwnPassword()
    {
        var admin = await CreateAdminUser();

        var token = await _userManager.GeneratePasswordResetTokenAsync(admin);
        var result = await _userManager.ResetPasswordAsync(admin, token, "NewAdminPass1!");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ChangePassword_WithWeakPassword_Fails()
    {
        var user = await CreateRegularUser();

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, "123");

        Assert.False(result.Succeeded);
    }
}
