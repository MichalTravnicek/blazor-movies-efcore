using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;

namespace BlazorWebAppMovies.Components.Handlers;

/// <summary>
/// DelegatingHandler that forwards the auth_token cookie from the
/// current HTTP context to outgoing requests. This lets server-side
/// Blazor pages call the JWT-protected API endpoints without
/// re-authenticating.
/// </summary>
public class AuthCookieHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthCookieHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            // Rebuild the URI with the real host from the current request.
            // This is needed because the named HttpClient's BaseAddress is a
            // placeholder, and the actual host/port varies by environment.
            if (request.RequestUri is not null)
            {
                var uriBuilder = new UriBuilder(request.RequestUri)
                {
                    Scheme = httpContext.Request.Scheme,
                    Host = httpContext.Request.Host.Host,
                    Port = httpContext.Request.Host.Port ??
                           (httpContext.Request.Scheme == "https" ? 443 : 80)
                };
                request.RequestUri = uriBuilder.Uri;
            }

            // Forward the auth cookie so the JWT middleware authenticates the request
            var authCookie = httpContext.Request.Cookies["auth_token"];
            if (!string.IsNullOrEmpty(authCookie))
            {
                request.Headers.Add("Cookie", $"auth_token={Uri.EscapeDataString(authCookie)}");
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
