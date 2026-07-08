using System.Net;
using BlazorWebAppMovies.Components.Handlers;
using Microsoft.AspNetCore.Http;
using Moq;

namespace BlazorWebAppMovies.Tests.Components;

public class AuthCookieHandlerTests
{
    private const string Scheme = "http";
    private const string Host = "localhost";
    private const int Port = 5216;

    private static Mock<IHttpContextAccessor> CreateAccessor(
        Action<Mock<HttpContext>>? configureContext = null)
    {
        var accessor = new Mock<IHttpContextAccessor>(MockBehavior.Strict);

        if (configureContext is not null)
        {
            var context = new Mock<HttpContext>(MockBehavior.Strict);
            configureContext(context);
            accessor.Setup(a => a.HttpContext).Returns(context.Object);
        }
        else
        {
            accessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        }

        return accessor;
    }

    private static Mock<HttpRequest> CreateRequestMock()
    {
        var request = new Mock<HttpRequest>(MockBehavior.Strict);
        request.Setup(r => r.Scheme).Returns(Scheme);
        request.Setup(r => r.Host).Returns(new HostString(Host, Port));
        return request;
    }

    private static Mock<IRequestCookieCollection> CreateCookieCollection(string? authToken)
    {
        var cookies = new Mock<IRequestCookieCollection>(MockBehavior.Strict);
        cookies.Setup(c => c["auth_token"]).Returns(authToken);
        return cookies;
    }

    private sealed class RequestCapture
    {
        public HttpRequestMessage? Request { get; set; }
    }

    /// <summary>
    /// Creates an HttpClient driven by AuthCookieHandler + a stub that captures
    /// the outgoing request. Returns the client and a capture object filled after SendAsync.
    /// </summary>
    private static (HttpClient Client, RequestCapture Capture) CreateClientWithCapture(
        IHttpContextAccessor accessor, string? baseAddress = null)
    {
        var capture = new RequestCapture();

        var stub = new StubHttpMessageHandler(request =>
        {
            capture.Request = request;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var authHandler = new AuthCookieHandler(accessor)
        {
            InnerHandler = stub
        };

        var client = new HttpClient(authHandler);

        if (baseAddress is not null)
        {
            client.BaseAddress = new Uri(baseAddress);
        }

        return (client, capture);
    }

    // ── Tests ─────────────────────────────────────────────────

    [Fact]
    public async Task RelativeUri_WithoutBaseAddress_ThrowsInvalidOperationException()
    {
        // Regression test: this is the exact error the app hit at startup.
        // HttpClient.SendAsync validates the URI before entering the handler
        // pipeline, so even with AuthCookieHandler in the chain, a relative
        // URI without a BaseAddress will throw.
        var accessor = CreateAccessor(); // HttpContext null, like an uninitialized circuit
        var (client, _) = CreateClientWithCapture(accessor.Object);

        // Use a relative URI — no BaseAddress set on the HttpClient
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/movies");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.SendAsync(request));

        Assert.Contains("invalid request URI", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RelativeUri_WithBaseAddress_SucceedsAndRewritesHost()
    {
        // Regression test: Blazor pages use relative URIs like "/api/movies"
        // with "BlazorApi" named client that has BaseAddress = "http://localhost".
        // This is the exact scenario that was failing at startup.
        var requestMock = CreateRequestMock();
        var cookies = CreateCookieCollection(null);
        requestMock.Setup(r => r.Cookies).Returns(cookies.Object);

        var accessor = CreateAccessor(ctx =>
            ctx.Setup(c => c.Request).Returns(requestMock.Object));

        var (client, capture) = CreateClientWithCapture(accessor.Object, "http://localhost");

        // Act — same pattern as Index.razor.LoadMovies()
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/movies");
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        // Should NOT throw InvalidOperationException
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var uri = capture.Request!.RequestUri!;
        Assert.Equal(Scheme, uri.Scheme);
        Assert.Equal(Host, uri.Host);
        Assert.Equal(Port, uri.Port);
        Assert.Equal("/api/movies", uri.AbsolutePath);
    }

    [Fact]
    public async Task RewritesHost_ToCurrentRequest()
    {
        // Arrange
        var requestMock = CreateRequestMock();
        var cookies = CreateCookieCollection(null);
        requestMock.Setup(r => r.Cookies).Returns(cookies.Object);

        var accessor = CreateAccessor(ctx =>
            ctx.Setup(c => c.Request).Returns(requestMock.Object));

        var (client, capture) = CreateClientWithCapture(accessor.Object);

        // Act
        await client.GetAsync("http://localhost/api/movies");

        // Assert
        var uri = capture.Request!.RequestUri!;
        Assert.Equal(Scheme, uri.Scheme);
        Assert.Equal(Host, uri.Host);
        Assert.Equal(Port, uri.Port);
        Assert.Equal("/api/movies", uri.AbsolutePath);
    }

    [Fact]
    public async Task PreservesPathAndQuery_WhenRewritingHost()
    {
        // Arrange
        var requestMock = CreateRequestMock();
        var cookies = CreateCookieCollection(null);
        requestMock.Setup(r => r.Cookies).Returns(cookies.Object);

        var accessor = CreateAccessor(ctx =>
            ctx.Setup(c => c.Request).Returns(requestMock.Object));

        var (client, capture) = CreateClientWithCapture(accessor.Object);

        // Act
        await client.GetAsync("http://localhost/api/movies/5?filter=test");

        // Assert
        Assert.Equal("/api/movies/5", capture.Request!.RequestUri!.AbsolutePath);
        Assert.Equal("?filter=test", capture.Request.RequestUri.Query);
    }

    [Fact]
    public async Task ForwardsAuthCookie_WhenPresent()
    {
        // Arrange
        var requestMock = CreateRequestMock();
        var cookies = CreateCookieCollection("my-jwt-token");
        requestMock.Setup(r => r.Cookies).Returns(cookies.Object);

        var accessor = CreateAccessor(ctx =>
            ctx.Setup(c => c.Request).Returns(requestMock.Object));

        var (client, capture) = CreateClientWithCapture(accessor.Object);

        // Act
        await client.GetAsync("http://localhost/api/movies");

        // Assert
        Assert.True(capture.Request!.Headers.Contains("Cookie"));
        var cookieHeader = Assert.Single(capture.Request.Headers.GetValues("Cookie"));
        Assert.Equal("auth_token=my-jwt-token", cookieHeader);
    }

    [Fact]
    public async Task SkipsCookie_WhenAbsent()
    {
        // Arrange
        var requestMock = CreateRequestMock();
        var cookies = CreateCookieCollection(null);
        requestMock.Setup(r => r.Cookies).Returns(cookies.Object);

        var accessor = CreateAccessor(ctx =>
            ctx.Setup(c => c.Request).Returns(requestMock.Object));

        var (client, capture) = CreateClientWithCapture(accessor.Object);

        // Act
        await client.GetAsync("http://localhost/api/movies");

        // Assert
        Assert.False(capture.Request!.Headers.Contains("Cookie"));
    }

    [Fact]
    public async Task PassesThrough_WhenHttpContextIsNull()
    {
        // Arrange
        var accessor = CreateAccessor(); // No configureContext → HttpContext is null
        var (client, capture) = CreateClientWithCapture(accessor.Object);

        // Act
        await client.GetAsync("http://localhost/api/movies");

        // Assert
        var uri = capture.Request!.RequestUri!;
        // URI unchanged — no rewrite when HttpContext is null
        Assert.Equal("http://localhost/api/movies", uri.AbsoluteUri);
        Assert.False(capture.Request.Headers.Contains("Cookie"));
    }

    [Fact]
    public async Task RewritesHostAndForwardsCookie_Together()
    {
        // Arrange
        var requestMock = CreateRequestMock();
        var cookies = CreateCookieCollection("session-token-123");
        requestMock.Setup(r => r.Cookies).Returns(cookies.Object);

        var accessor = CreateAccessor(ctx =>
            ctx.Setup(c => c.Request).Returns(requestMock.Object));

        var (client, capture) = CreateClientWithCapture(accessor.Object);

        // Act
        await client.GetAsync("http://localhost/api/movies/42");

        // Assert — both behaviors applied
        var uri = capture.Request!.RequestUri!;
        Assert.Equal(Scheme, uri.Scheme);
        Assert.Equal(Host, uri.Host);
        Assert.Equal(Port, uri.Port);
        Assert.Equal("/api/movies/42", uri.AbsolutePath);

        var cookieHeader = Assert.Single(capture.Request.Headers.GetValues("Cookie"));
        Assert.Equal("auth_token=session-token-123", cookieHeader);
    }

    // ── Helpers ────────────────────────────────────────────────

    /// <summary>
    /// A lightweight HttpMessageHandler that invokes a delegate.
    /// Sits at the innermost position of the handler pipeline.
    /// </summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
