using System.Security.Claims;
using BlazorWebAppMovies.Controllers;
using BlazorWebAppMovies.Data;
using BlazorWebAppMovies.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace BlazorWebAppMovies.Tests.Controllers;

public class AdminControllerTests : IDisposable
{
    private readonly string _dbName;
    private readonly BlazorWebAppMoviesContext _context;
    private readonly UserManager<User> _userManager;
    private readonly AdminController _controller;
    private readonly IServiceScope _scope;

    public AdminControllerTests()
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

        _controller = new AdminController(_userManager);

        // Set up an admin principal on the controller
        var admin = CreateAdminUser().GetAwaiter().GetResult();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, admin.Id),
            new(ClaimTypes.Name, admin.UserName!),
            new(ClaimTypes.Email, admin.Email!),
            new("role", "Admin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth", ClaimTypes.Name, "role");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    public void Dispose()
    {
        _scope.Dispose();
        _context.Dispose();
    }

    private async Task<User> CreateAdminUser()
    {
        // Seed roles
        var roleManager = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync("Admin"))
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        if (!await roleManager.RoleExistsAsync("User"))
            await roleManager.CreateAsync(new IdentityRole("User"));

        var admin = new User
        {
            UserName = "admin@test.com",
            Email = "admin@test.com",
            Name = "Admin User"
        };
        var result = await _userManager.CreateAsync(admin, "Admin123!");
        if (result.Succeeded)
            await _userManager.AddToRoleAsync(admin, "Admin");

        return admin;
    }

    private async Task<User> CreateRegularUser(string email = "user@test.com", string name = "Regular User")
    {
        var user = new User
        {
            UserName = email,
            Email = email,
            Name = name
        };
        var result = await _userManager.CreateAsync(user, "User1234!");
        if (result.Succeeded)
            await _userManager.AddToRoleAsync(user, "User");
        return user;
    }

    // ── GET /api/admin/users ─────────────────────────────────────

    [Fact]
    public async Task GetUsers_ReturnsAllUsers()
    {
        await CreateAdminUser();
        await CreateRegularUser("alice@test.com", "Alice");
        await CreateRegularUser("bob@test.com", "Bob");

        var result = await _controller.GetUsers();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var users = Assert.IsType<List<UserDto>>(okResult.Value);
        Assert.Equal(3, users.Count); // admin + alice + bob
    }

    [Fact]
    public async Task GetUsers_ReturnsUsersOrderedByName()
    {
        await CreateRegularUser("z@test.com", "Zelda");
        await CreateRegularUser("a@test.com", "Aaron");

        var result = await _controller.GetUsers();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var users = Assert.IsType<List<UserDto>>(okResult.Value);
        Assert.Equal("Aaron", users[0].Name);
        Assert.Equal("Zelda", users[^1].Name);
    }

    [Fact]
    public async Task GetUsers_IncludesCorrectRoles()
    {
        // admin is already created in constructor, just add a regular user
        await CreateRegularUser();

        var result = await _controller.GetUsers();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var users = Assert.IsType<List<UserDto>>(okResult.Value);
        var adminDto = users.Single(u => u.Email == "admin@test.com");
        Assert.Contains("Admin", adminDto.Roles);
        Assert.True(adminDto.IsAdmin);
    }

    [Fact]
    public async Task GetUsers_ReturnsUserDtoShape()
    {
        var user = await CreateRegularUser();

        var result = await _controller.GetUsers();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var users = Assert.IsType<List<UserDto>>(okResult.Value);
        var dto = users.Single(u => u.Id == user.Id);

        Assert.Equal(user.Id, dto.Id);
        Assert.Equal(user.Name, dto.Name);
        Assert.Equal(user.Email, dto.Email);
        Assert.Contains("User", dto.Roles);
        Assert.False(dto.IsAdmin);
    }

    // ── POST /api/admin/users ────────────────────────────────────

    [Fact]
    public async Task CreateUser_WithValidData_ReturnsOk()
    {
        var request = new AdminController.CreateUserRequest(
            "New User", "new@test.com", "NewUser123!", "User");

        var result = await _controller.CreateUser(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task CreateUser_CreatesUserInDatabase()
    {
        var request = new AdminController.CreateUserRequest(
            "New User", "new@test.com", "NewUser123!", "User");

        await _controller.CreateUser(request);

        var user = await _userManager.FindByEmailAsync("new@test.com");
        Assert.NotNull(user);
        Assert.Equal("New User", user.Name);
    }

    [Fact]
    public async Task CreateUser_WithDuplicateEmail_ReturnsConflict()
    {
        await CreateRegularUser("dup@test.com", "Original");

        var request = new AdminController.CreateUserRequest(
            "Duplicate", "dup@test.com", "Pass1234!", "User");

        var result = await _controller.CreateUser(request);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task CreateUser_AssignsCorrectRole()
    {
        var request = new AdminController.CreateUserRequest(
            "Admin Guy", "admin@new.com", "Admin123!", "Admin");

        await _controller.CreateUser(request);

        var user = await _userManager.FindByEmailAsync("admin@new.com");
        Assert.NotNull(user);
        Assert.True(await _userManager.IsInRoleAsync(user, "Admin"));
    }

    [Fact]
    public async Task CreateUser_WithWeakPassword_ReturnsBadRequest()
    {
        var request = new AdminController.CreateUserRequest(
            "Weak", "weak@test.com", "123", "User");

        var result = await _controller.CreateUser(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── PUT /api/admin/users/{id} ────────────────────────────────

    [Fact]
    public async Task UpdateUser_WithValidData_ReturnsOk()
    {
        var user = await CreateRegularUser();

        var request = new AdminController.UpdateUserRequest(
            "Updated Name", "updated@test.com", "Admin");

        var result = await _controller.UpdateUser(user.Id, request);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task UpdateUser_UpdatesFieldsInDatabase()
    {
        var user = await CreateRegularUser();

        var request = new AdminController.UpdateUserRequest(
            "Updated Name", "updated@test.com", "Admin");

        await _controller.UpdateUser(user.Id, request);

        var updated = await _userManager.FindByIdAsync(user.Id);
        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated.Name);
        Assert.Equal("updated@test.com", updated.Email);
        Assert.True(await _userManager.IsInRoleAsync(updated, "Admin"));
    }

    [Fact]
    public async Task UpdateUser_WithNonExistentId_ReturnsNotFound()
    {
        var request = new AdminController.UpdateUserRequest(
            "Ghost", "ghost@test.com", "User");

        var result = await _controller.UpdateUser("nonexistent-id", request);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task UpdateUser_WithDuplicateEmail_ReturnsConflict()
    {
        await CreateRegularUser("existing@test.com", "Existing");
        var user = await CreateRegularUser("other@test.com", "Other");

        var request = new AdminController.UpdateUserRequest(
            "Other", "existing@test.com", "User");

        var result = await _controller.UpdateUser(user.Id, request);

        Assert.IsType<ConflictObjectResult>(result);
    }

    // ── PUT /api/admin/users/{id}/password ──────────────────────

    [Fact]
    public async Task ChangePassword_WithValidData_ReturnsOk()
    {
        var user = await CreateRegularUser();

        var request = new AdminController.ChangePasswordRequest("NewPass123!");
        var result = await _controller.ChangePassword(user.Id, request);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ChangePassword_ActuallyChangesPassword()
    {
        var user = await CreateRegularUser();

        var request = new AdminController.ChangePasswordRequest("NewPass123!");
        await _controller.ChangePassword(user.Id, request);

        // Verify by signing in with the new password
        var signInManager = CreateSignInManager();
        var signInResult = await signInManager.CheckPasswordSignInAsync(user, "NewPass123!", false);
        Assert.True(signInResult.Succeeded);
    }

    [Fact]
    public async Task ChangePassword_WithNonExistentId_ReturnsNotFound()
    {
        var request = new AdminController.ChangePasswordRequest("NewPass123!");
        var result = await _controller.ChangePassword("nonexistent-id", request);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ChangePassword_WithWeakPassword_ReturnsBadRequest()
    {
        var user = await CreateRegularUser();

        var request = new AdminController.ChangePasswordRequest("123");
        var result = await _controller.ChangePassword(user.Id, request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── DELETE /api/admin/users/{id} ────────────────────────────

    [Fact]
    public async Task DeleteUser_WithExistingId_ReturnsOk()
    {
        var user = await CreateRegularUser();

        var result = await _controller.DeleteUser(user.Id);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task DeleteUser_RemovesUserFromDatabase()
    {
        var user = await CreateRegularUser();

        await _controller.DeleteUser(user.Id);

        var deleted = await _userManager.FindByIdAsync(user.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteUser_WithNonExistentId_ReturnsNotFound()
    {
        var result = await _controller.DeleteUser("nonexistent-id");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ── Helpers ─────────────────────────────────────────────────

    private SignInManager<User> CreateSignInManager()
    {
        var httpContextAccessor = Mock.Of<IHttpContextAccessor>();
        var userPrincipalFactory = Mock.Of<IUserClaimsPrincipalFactory<User>>();
        var identityOptions = Microsoft.Extensions.Options.Options.Create(new IdentityOptions());
        var logger = Mock.Of<ILogger<SignInManager<User>>>();
        var schemeProvider = Mock.Of<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>();
        var userConfirmation = Mock.Of<IUserConfirmation<User>>();

        return new SignInManager<User>(
            _userManager,
            httpContextAccessor,
            userPrincipalFactory,
            identityOptions,
            logger,
            schemeProvider,
            userConfirmation);
    }
}
