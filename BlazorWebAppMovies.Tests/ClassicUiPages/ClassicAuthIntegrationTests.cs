using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BlazorWebAppMovies.Controllers;
using BlazorWebAppMovies.Data;
using BlazorWebAppMovies.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace BlazorWebAppMovies.Tests.ClassicUiPages;

/// <summary>
/// Tests that verify the auth flow used by the Classic UI:
/// login via /api/auth/login, JWT token stored in cookie, roles correctly assigned.
/// </summary>
public class ClassicAuthIntegrationTests : IDisposable
{
    private const string StaticJwtKey = "ThisIsAStaticTestKeyThatIsExactly32Bytes!!";
    private const string JwtIssuer = "TestIssuer";
    private const string JwtAudience = "TestAudience";

    private readonly BlazorWebAppMoviesContext _context;
    private readonly UserManager<User> _userManager;
    private readonly AuthController _authController;
    private readonly IServiceScope _scope;
    private readonly DefaultHttpContext _httpContext;

    public ClassicAuthIntegrationTests()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();

        services.AddDbContext<BlazorWebAppMoviesContext>(options =>
            options.UseInMemoryDatabase(dbName));

        services.AddDbContextFactory<BlazorWebAppMoviesContext>(options =>
            options.UseInMemoryDatabase(dbName));

        services.AddIdentity<User, IdentityRole>()
            .AddEntityFrameworkStores<BlazorWebAppMoviesContext>()
            .AddDefaultTokenProviders();

        services.AddLogging();

        var serviceProvider = services.BuildServiceProvider();
        _scope = serviceProvider.CreateScope();

        _context = _scope.ServiceProvider.GetRequiredService<BlazorWebAppMoviesContext>();
        _userManager = _scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = StaticJwtKey,
            ["Jwt:Issuer"] = JwtIssuer,
            ["Jwt:Audience"] = JwtAudience
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var mockLogger = new Mock<ILogger<AuthController>>();
        var signInManager = CreateSignInManager();

        _authController = new AuthController(
            _userManager,
            signInManager,
            configuration);

        _httpContext = new DefaultHttpContext();
        _authController.ControllerContext = new ControllerContext
        {
            HttpContext = _httpContext
        };

        // Seed roles
        SeedRoles().GetAwaiter().GetResult();
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

    private SignInManager<User> CreateSignInManager()
    {
        var httpContextAccessor = Mock.Of<IHttpContextAccessor>();
        var userPrincipalFactory = Mock.Of<IUserClaimsPrincipalFactory<User>>();
        var identityOptions = Options.Create(new IdentityOptions());
        var logger = Mock.Of<ILogger<SignInManager<User>>>();
        var schemeProvider = Mock.Of<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>();
        var userConfirmation = Mock.Of<Microsoft.AspNetCore.Identity.IUserConfirmation<User>>();

        return new SignInManager<User>(
            _userManager,
            httpContextAccessor,
            userPrincipalFactory,
            identityOptions,
            logger,
            schemeProvider,
            userConfirmation);
    }

    private string? ExtractTokenFromCookie()
    {
        if (_httpContext.Response.Headers.TryGetValue("Set-Cookie", out var setCookie))
        {
            foreach (var cookie in setCookie)
            {
                if (cookie != null && cookie.StartsWith("auth_token="))
                {
                    var parts = cookie.Split(';')[0];
                    return parts["auth_token=".Length..];
                }
            }
        }
        return null;
    }

    private static JwtSecurityToken DecodeToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(StaticJwtKey);

        handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = JwtIssuer,
            ValidAudience = JwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(key)
        }, out var validatedToken);

        return (JwtSecurityToken)validatedToken;
    }

    // ── Login (used by Classic UI login modal) ──────────────────

    [Fact]
    public async Task Login_WithValidCredentials_SetsAuthCookie()
    {
        var user = new User { UserName = "classic@test.com", Email = "classic@test.com", Name = "Classic User" };
        await _userManager.CreateAsync(user, "Pass1234!");
        await _userManager.AddToRoleAsync(user, "User");

        var request = new AuthController.LoginRequest("classic@test.com", "Pass1234!");
        await _authController.Login(request);

        var token = ExtractTokenFromCookie();
        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOk()
    {
        var user = new User { UserName = "ok@test.com", Email = "ok@test.com", Name = "OK User" };
        await _userManager.CreateAsync(user, "Pass1234!");
        await _userManager.AddToRoleAsync(user, "User");

        var request = new AuthController.LoginRequest("ok@test.com", "Pass1234!");
        var result = await _authController.Login(request);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        var user = new User { UserName = "fail@test.com", Email = "fail@test.com", Name = "Fail User" };
        await _userManager.CreateAsync(user, "Pass1234!");

        var request = new AuthController.LoginRequest("fail@test.com", "WrongPass123!");
        var result = await _authController.Login(request);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_WithAdminRole_SetsRoleClaim()
    {
        var admin = new User { UserName = "admin@classic.com", Email = "admin@classic.com", Name = "Admin Classic" };
        await _userManager.CreateAsync(admin, "Admin123!");
        await _userManager.AddToRoleAsync(admin, "Admin");

        var request = new AuthController.LoginRequest("admin@classic.com", "Admin123!");
        await _authController.Login(request);

        var tokenStr = ExtractTokenFromCookie();
        Assert.NotNull(tokenStr);

        var token = DecodeToken(tokenStr);
        var roleClaims = token.Claims.Where(c => c.Type == "role").Select(c => c.Value).ToList();
        Assert.Contains("Admin", roleClaims);
    }

    [Fact]
    public async Task Login_WithUserRole_DoesNotContainAdminRole()
    {
        var user = new User { UserName = "user@classic.com", Email = "user@classic.com", Name = "User Classic" };
        await _userManager.CreateAsync(user, "Pass1234!");
        await _userManager.AddToRoleAsync(user, "User");

        var request = new AuthController.LoginRequest("user@classic.com", "Pass1234!");
        await _authController.Login(request);

        var tokenStr = ExtractTokenFromCookie();
        Assert.NotNull(tokenStr);

        var token = DecodeToken(tokenStr);
        var roleClaims = token.Claims.Where(c => c.Type == "role").Select(c => c.Value).ToList();
        Assert.DoesNotContain("Admin", roleClaims);
        Assert.Contains("User", roleClaims);
    }

    [Fact]
    public async Task Login_SetsCookieAsHttpOnly()
    {
        var user = new User { UserName = "httponly@test.com", Email = "httponly@test.com", Name = "HttpOnly User" };
        await _userManager.CreateAsync(user, "Pass1234!");

        var request = new AuthController.LoginRequest("httponly@test.com", "Pass1234!");
        await _authController.Login(request);

        Assert.True(_httpContext.Response.Headers.TryGetValue("Set-Cookie", out var setCookie));
        var cookie = setCookie.FirstOrDefault(c => c!.StartsWith("auth_token="));
        Assert.NotNull(cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
    }

    // ── Logout ──────────────────────────────────────────────────

    [Fact]
    public void Logout_ExpiresCookie()
    {
        var result = _authController.Logout();

        Assert.IsType<OkObjectResult>(result);

        // Check that the cookie is expired
        Assert.True(_httpContext.Response.Headers.TryGetValue("Set-Cookie", out var setCookie));
        var cookie = setCookie.FirstOrDefault(c => c!.StartsWith("auth_token="));
        Assert.NotNull(cookie);
        Assert.Contains("expires=Thu, 01 Jan 1970", cookie, StringComparison.OrdinalIgnoreCase);
    }
}
