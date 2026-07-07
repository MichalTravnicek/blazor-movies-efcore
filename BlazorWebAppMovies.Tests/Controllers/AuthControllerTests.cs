using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BlazorWebAppMovies.Controllers;
using BlazorWebAppMovies.Data;
using BlazorWebAppMovies.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace BlazorWebAppMovies.Tests.Controllers;

public class AuthControllerTests : IDisposable
{
    private const string StaticJwtKey = "ThisIsAStaticTestKeyThatIsExactly32Bytes!!";
    private const string JwtIssuer = "TestIssuer";
    private const string JwtAudience = "TestAudience";

    private readonly BlazorWebAppMoviesContext _context;
    private readonly UserManager<User> _userManager;
    private readonly AuthController _controller;
    private readonly IServiceScope _scope;
    private readonly DefaultHttpContext _httpContext;

    public AuthControllerTests()
    {
        var services = new ServiceCollection();

        services.AddDbContext<BlazorWebAppMoviesContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

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

        _controller = new AuthController(
            _userManager,
            signInManager,
            configuration);

        // Set up HttpContext so Response.Cookies.Append works
        _httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = _httpContext
        };
    }

    public void Dispose()
    {
        _context.Dispose();
        _scope.Dispose();
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

    // ── Register tests ────────────────────────────────────────────

    [Fact]
    public async Task Register_WithValidData_ReturnsOk()
    {
        var request = new AuthController.RegisterRequest(
            "Jane Doe", "jane@example.com", "P@ssw0rd!");

        var result = await _controller.Register(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task Register_CreatesUserInDatabase()
    {
        var request = new AuthController.RegisterRequest(
            "John Smith", "john@example.com", "P@ssw0rd!");

        await _controller.Register(request);

        var user = await _userManager.FindByEmailAsync("john@example.com");
        Assert.NotNull(user);
        Assert.Equal("John Smith", user.Name);
        Assert.Equal("john@example.com", user.UserName);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        var request = new AuthController.RegisterRequest(
            "Alice", "alice@example.com", "P@ssw0rd!");
        await _controller.Register(request);

        var duplicate = new AuthController.RegisterRequest(
            "Alice Dup", "alice@example.com", "OtherP@ss1");

        var result = await _controller.Register(duplicate);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task Register_WithWeakPassword_ReturnsBadRequest()
    {
        var request = new AuthController.RegisterRequest(
            "Weak", "weak@example.com", "123");

        var result = await _controller.Register(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Register_HashesPassword_DoesNotStorePlaintext()
    {
        var request = new AuthController.RegisterRequest(
            "Secure", "secure@example.com", "C0mpl3x!Pass");

        await _controller.Register(request);

        var user = await _userManager.FindByEmailAsync("secure@example.com");
        Assert.NotNull(user);
        Assert.NotEqual("C0mpl3x!Pass", user.PasswordHash);
        Assert.StartsWith("AQAAAA", user.PasswordHash); // ASP.NET Identity hash prefix
    }

    // ── Login tests ───────────────────────────────────────────────

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithToken()
    {
        var email = "login-test@example.com";
        var password = "ValidP@ss1";

        await _userManager.CreateAsync(new User
        {
            UserName = email,
            Email = email,
            Name = "Login Test"
        }, password);

        var request = new AuthController.LoginRequest(email, password);

        var result = await _controller.Login(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        var token = ExtractTokenFromCookie();
        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsValidJwtToken()
    {
        var email = "jwt-test@example.com";
        var password = "JwtT3st!Pass";

        await _userManager.CreateAsync(new User
        {
            UserName = email,
            Email = email,
            Name = "JWT Test"
        }, password);

        var request = new AuthController.LoginRequest(email, password);

        var result = await _controller.Login(request);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var token = ExtractTokenFromCookie();

        Assert.NotNull(token);

        var jwt = DecodeToken(token);
        Assert.Equal(JwtIssuer, jwt.Issuer);
        Assert.Contains(jwt.Audiences, a => a == JwtAudience);
    }

    [Fact]
    public async Task Login_WithValidCredentials_TokenContainsExpectedClaims()
    {
        var email = "claims-test@example.com";
        var password = "Cla1ms!T3st";

        await _userManager.CreateAsync(new User
        {
            UserName = email,
            Email = email,
            Name = "Claims Test"
        }, password);

        var request = new AuthController.LoginRequest(email, password);
        var result = await _controller.Login(request);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var token = ExtractTokenFromCookie();

        Assert.NotNull(token);
        var jwt = DecodeToken(token);

        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Email && c.Value == email);
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Name && c.Value == "Claims Test");
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var email = "wrong-pw@example.com";

        await _userManager.CreateAsync(new User
        {
            UserName = email,
            Email = email,
            Name = "Wrong PW"
        }, "C0rrect!Pass");

        var request = new AuthController.LoginRequest(email, "WrongPassword1!");

        var result = await _controller.Login(request);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_WithNonexistentEmail_ReturnsUnauthorized()
    {
        var request = new AuthController.LoginRequest(
            "nobody@example.com", "AnyPass1!");

        var result = await _controller.Login(request);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_TokenHasExpClaim()
    {
        var email = "expiry-test@example.com";
        var password = "Exp1ry!T3st";

        await _userManager.CreateAsync(new User
        {
            UserName = email,
            Email = email,
            Name = "Expiry Test"
        }, password);

        var request = new AuthController.LoginRequest(email, password);
        var result = await _controller.Login(request);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var token = ExtractTokenFromCookie();

        Assert.NotNull(token);
        var jwt = DecodeToken(token);

        var expClaim = jwt.Claims.FirstOrDefault(c => c.Type == "exp");
        Assert.NotNull(expClaim);

        var expUnix = long.Parse(expClaim.Value);
        var expTime = DateTime.UnixEpoch.AddSeconds(expUnix);
        var now = DateTime.UtcNow;

        Assert.True(expTime > now, "Token expiration should be in the future");
        var diff = expTime - now;
        Assert.True(diff is { Hours: >= 23 } and { Hours: <= 25 },
            $"Expected ~24h expiry but got {diff.TotalHours:F1}h");
    }

    // ── Logout tests ──────────────────────────────────────────────

    [Fact]
    public async Task Logout_ReturnsOk()
    {
        var email = "logout-test@example.com";
        var password = "LogoutT3st!";

        await _userManager.CreateAsync(new User
        {
            UserName = email,
            Email = email,
            Name = "Logout Test"
        }, password);

        // First login to populate the cookie
        var loginRequest = new AuthController.LoginRequest(email, password);
        await _controller.Login(loginRequest);

        var result = _controller.Logout();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task Logout_ClearsAuthCookie()
    {
        var email = "logout-cookie@example.com";
        var password = "C00kie!Cl3ar";

        await _userManager.CreateAsync(new User
        {
            UserName = email,
            Email = email,
            Name = "Logout Cookie"
        }, password);

        // First login to populate the cookie
        var loginRequest = new AuthController.LoginRequest(email, password);
        await _controller.Login(loginRequest);

        _controller.Logout();

        var setCookie = _httpContext.Response.Headers["Set-Cookie"];
        // Find the last auth_token cookie (the one set by Logout, which overrides the login one)
        var authCookies = setCookie.Where(c => c != null && c.StartsWith("auth_token=", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.NotEmpty(authCookies);
        var lastAuthCookie = authCookies.Last()!;
        Assert.Contains("max-age=0", lastAuthCookie, StringComparison.OrdinalIgnoreCase);
    }
}
