using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AngleSharp.Dom;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using BlazorWebAppMovies.Models;

namespace BlazorWebAppMovies.Tests.Bunit;

/// <summary>
/// Base class for bUnit component tests.
/// Provides a pre-configured TestContext with:
/// - Mocked IHttpClientFactory returning pre-configured JSON responses
/// - bUnit's built-in FakeNavigationManager (URI-aware, supports query params)
/// - Mocked IJSRuntime that handles QuickGrid interop and general JS calls
/// - Authentication state helpers
/// </summary>
public abstract class BunitTestBase : IDisposable
{
    protected readonly TestContext Ctx;
    protected readonly Mock<IHttpClientFactory> HttpClientFactoryMock;
    protected readonly Mock<IJSRuntime> JsRuntimeMock;
    protected readonly MockHttpMessageHandler MockHttp;
    private FakeNavigationManager? _navManager;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    protected BunitTestBase()
    {
        Ctx = new TestContext();
        MockHttp = new MockHttpMessageHandler();
        HttpClientFactoryMock = new Mock<IHttpClientFactory>();
        JsRuntimeMock = new Mock<IJSRuntime>(MockBehavior.Loose);

        // Set up IJSRuntime to return a mock IJSObjectReference for any InvokeAsync<IJSObjectReference> call.
        // This is needed by QuickGrid which calls JS interop in OnAfterRenderAsync.
        var mockJsObjRef = new Mock<IJSObjectReference>(MockBehavior.Loose);
        JsRuntimeMock
            .Setup(r => r.InvokeAsync<IJSObjectReference>(It.IsAny<string>(), It.IsAny<object[]>()))
            .ReturnsAsync(mockJsObjRef.Object);

        // Create an HttpClient backed by the mock handler
        var httpClient = new HttpClient(MockHttp)
        {
            BaseAddress = new Uri("http://localhost")
        };

        HttpClientFactoryMock
            .Setup(f => f.CreateClient("BlazorApi"))
            .Returns(httpClient);

        // Register services BEFORE anything uses the service provider
        Ctx.Services.AddSingleton(HttpClientFactoryMock.Object);
        Ctx.Services.AddSingleton(JsRuntimeMock.Object);

        // Add Blazor's built-in auth services so CascadingAuthenticationState works
        Ctx.Services.AddAuthorization();
        Ctx.Services.AddCascadingAuthenticationState();

        // Register a minimal UserManager (needed by Home.razor even for unauthenticated paths)
        Ctx.Services.AddSingleton(CreateEmptyUserManager());

        // Register FakeNavigationManager explicitly so components can inject it
        Ctx.Services.AddSingleton<NavigationManager, FakeNavigationManager>();
    }

    /// <summary>
    /// Gets the FakeNavigationManager after it's been resolved by the service provider.
    /// </summary>
    protected FakeNavigationManager NavManager
    {
        get
        {
            if (_navManager is null)
            {
                _navManager = (FakeNavigationManager)Ctx.Services.GetRequiredService<NavigationManager>();
            }
            return _navManager;
        }
    }

    /// <summary>
    /// Creates a minimal UserManager for components that inject it but don't call it.
    /// Home.razor injects UserManager for the authenticated path but still needs
    /// it registered even when rendered unauthenticated.
    /// </summary>
    private static UserManager<User> CreateEmptyUserManager()
    {
        var store = Mock.Of<IUserStore<User>>();
        return new UserManager<User>(
            store,
            Mock.Of<Microsoft.Extensions.Options.IOptions<IdentityOptions>>(),
            Mock.Of<IPasswordHasher<User>>(),
            Array.Empty<IUserValidator<User>>(),
            Array.Empty<IPasswordValidator<User>>(),
            Mock.Of<ILookupNormalizer>(),
            Mock.Of<IdentityErrorDescriber>(),
            Mock.Of<IServiceProvider>(),
            Mock.Of<ILogger<UserManager<User>>>()
        );
    }

    public void Dispose()
    {
        Ctx.Dispose();
    }

    /// <summary>
    /// Sets up the authentication state for the test context.
    /// Must be called BEFORE rendering any components (before service provider is locked).
    /// </summary>
    protected void SetAuthState(bool isAuthenticated, string? role = null, string? userName = "test@user.com")
    {
        if (!isAuthenticated)
        {
            var anonymous = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            Ctx.Services.AddSingleton<AuthenticationStateProvider>(new TestAuthStateProvider(anonymous));
            return;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, userName!),
            new(ClaimTypes.NameIdentifier, "test-user-id"),
            new(ClaimTypes.Email, userName!)
        };

        if (role is not null)
        {
            claims.Add(new Claim("role", role));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth", ClaimTypes.Name, "role");
        var principal = new ClaimsPrincipal(identity);
        var authenticated = new AuthenticationState(principal);
        Ctx.Services.AddSingleton<AuthenticationStateProvider>(new TestAuthStateProvider(authenticated));
    }

    /// <summary>
    /// Sets up a JSON response for a given HTTP method and path.
    /// </summary>
    protected void RespondJson<T>(HttpMethod method, string path, T data, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        MockHttp.RespondTo(method, path, new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
    }

    /// <summary>
    /// Sets up an empty response (no content body).
    /// </summary>
    protected void RespondEmpty(HttpMethod method, string path, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        MockHttp.RespondTo(method, path, new HttpResponseMessage(statusCode));
    }

    /// <summary>
    /// Navigates the FakeNavigationManager to the given URI (sets query params for SupplyParameterFromQuery).
    /// </summary>
    protected void NavigateTo(string uri)
    {
        NavManager.NavigateTo(uri);
    }

    /// <summary>
    /// Asserts that a rendered component contains the specified text.
    /// </summary>
    protected static void AssertContains(string expectedText, IRenderedFragment renderedComponent)
    {
        var markup = renderedComponent.Markup;
        Assert.Contains(expectedText, markup, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Asserts that a rendered component does NOT contain the specified text.
    /// </summary>
    protected static void AssertNotContains(string unexpectedText, IRenderedFragment renderedComponent)
    {
        var markup = renderedComponent.Markup;
        Assert.DoesNotContain(unexpectedText, markup, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Asserts that the component renders an element matching the CSS selector.
    /// </summary>
    protected static void AssertElementExists(string cssSelector, IRenderedFragment renderedComponent)
    {
        var element = renderedComponent.Find(cssSelector);
        Assert.NotNull(element);
    }

    /// <summary>
    /// Asserts that no element matching the CSS selector exists in the rendered component.
    /// </summary>
    protected static void AssertElementNotExists(string cssSelector, IRenderedFragment renderedComponent)
    {
        var count = renderedComponent.FindAll(cssSelector).Count;
        Assert.Equal(0, count);
    }
}

/// <summary>
/// Mock HTTP message handler that returns pre-configured responses.
/// </summary>
public class MockHttpMessageHandler : DelegatingHandler
{
    private readonly Dictionary<string, HttpResponseMessage> _responses = new();

    public void RespondTo(HttpMethod method, string path, HttpResponseMessage response)
    {
        // Normalize: remove leading slash for consistency
        var normalizedPath = path.TrimStart('/');
        var key = $"{method.Method}:{normalizedPath}";
        _responses[key] = response;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath?.TrimStart('/') ?? "";
        var key = $"{request.Method}:{path}";

        if (_responses.TryGetValue(key, out var response))
        {
            return Task.FromResult(response);
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{\"message\":\"No mock configured for this request\"}", Encoding.UTF8, "application/json")
        });
    }
}

/// <summary>
/// Simple test authentication state provider.
/// </summary>
internal class TestAuthStateProvider : AuthenticationStateProvider
{
    private readonly AuthenticationState _state;

    public TestAuthStateProvider(AuthenticationState state)
    {
        _state = state;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(_state);
    }
}
