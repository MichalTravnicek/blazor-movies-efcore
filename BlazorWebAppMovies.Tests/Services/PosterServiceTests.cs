using System.Net;
using System.Text.Json;
using BlazorWebAppMovies.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace BlazorWebAppMovies.Tests.Services;

public class PosterServiceTests
{
    private static (PosterService Service, Mock<HttpMessageHandler> Handler) CreateService(
        string? apiKey = "fake-key",
        string? tmdbResponse = null,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        if (tmdbResponse != null)
        {
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(tmdbResponse)
                });
        }

        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://api.themoviedb.org")
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tmdb:ApiKey"] = apiKey,
                ["Tmdb:ImageBaseUrl"] = "https://image.tmdb.org/t/p/"
            })
            .Build();

        var env = Mock.Of<IWebHostEnvironment>(e =>
            e.WebRootPath == Path.GetTempPath());

        var logger = Mock.Of<ILogger<PosterService>>();

        var service = new PosterService(httpClient, config, env, logger);
        return (service, handler);
    }

    // ── FetchPosterUrlAsync ─────────────────────────────────────

    [Fact]
    public async Task FetchPosterUrlAsync_WithValidMovie_ReturnsFullUrl()
    {
        var json = JsonSerializer.Serialize(new
        {
            results = new[]
            {
                new { poster_path = "/abc123.jpg" }
            }
        });

        var (service, _) = CreateService(tmdbResponse: json);
        var url = await service.FetchPosterUrlAsync("Mad Max", 2015);

        Assert.NotNull(url);
        Assert.Contains("image.tmdb.org", url);
        Assert.Contains("w500", url);
        Assert.Contains("abc123.jpg", url);
    }

    [Fact]
    public async Task FetchPosterUrlAsync_WithNoResults_ReturnsNull()
    {
        var json = JsonSerializer.Serialize(new
        {
            results = Array.Empty<object>()
        });

        var (service, _) = CreateService(tmdbResponse: json);
        var url = await service.FetchPosterUrlAsync("NonExistentMovie", 9999);

        Assert.Null(url);
    }

    [Fact]
    public async Task FetchPosterUrlAsync_WithNoApiKey_ReturnsNull()
    {
        var (service, _) = CreateService(apiKey: null);
        var url = await service.FetchPosterUrlAsync("Mad Max", 2015);

        Assert.Null(url);
    }

    [Fact]
    public async Task FetchPosterUrlAsync_WithEmptyApiKey_ReturnsNull()
    {
        var (service, _) = CreateService(apiKey: "");
        var url = await service.FetchPosterUrlAsync("Mad Max", 2015);

        Assert.Null(url);
    }

    [Fact]
    public async Task FetchPosterUrlAsync_WithApiError_ReturnsNull()
    {
        var (service, _) = CreateService(tmdbResponse: "{}", statusCode: HttpStatusCode.InternalServerError);
        var url = await service.FetchPosterUrlAsync("Mad Max", 2015);

        Assert.Null(url);
    }

    [Fact]
    public async Task FetchPosterUrlAsync_WithoutYear_StillWorks()
    {
        var json = JsonSerializer.Serialize(new
        {
            results = new[]
            {
                new { poster_path = "/xyz.webp" }
            }
        });

        var (service, _) = CreateService(tmdbResponse: json);
        var url = await service.FetchPosterUrlAsync("Mad Max", null);

        Assert.NotNull(url);
        Assert.Contains("xyz.webp", url);
    }

    [Fact]
    public async Task FetchPosterUrlAsync_WithNullResults_ReturnsNull()
    {
        var json = JsonSerializer.Serialize(new
        {
            results = (object?)null
        });

        var (service, _) = CreateService(tmdbResponse: json);
        var url = await service.FetchPosterUrlAsync("Mad Max", 2015);

        Assert.Null(url);
    }

    // ── SavePosterAsync ─────────────────────────────────────────

    [Fact]
    public async Task SavePosterAsync_SavesFileAndReturnsUrl()
    {
        var uploadsDir = Path.Combine(Path.GetTempPath(), "uploads", "posters");
        Directory.CreateDirectory(uploadsDir);

        try
        {
            var env = Mock.Of<IWebHostEnvironment>(e =>
                e.WebRootPath == Path.GetTempPath());

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Tmdb:ApiKey"] = "test-key",
                    ["Tmdb:ImageBaseUrl"] = "https://image.tmdb.org/t/p/"
                })
                .Build();

            var logger = Mock.Of<ILogger<PosterService>>();

            var httpClient = new HttpClient();
            var service = new PosterService(httpClient, config, env, logger);

            var fileMock = new Mock<IFormFile>();
            var content = "fake-image-bytes";
            var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            fileMock.Setup(f => f.OpenReadStream()).Returns(ms);
            fileMock.Setup(f => f.Length).Returns(ms.Length);
            fileMock.Setup(f => f.FileName).Returns("test.jpg");

            var result = await service.SavePosterAsync(42, fileMock.Object);

            Assert.NotNull(result);
            Assert.Contains("uploads/posters/42_full.webp", result);

            // Verify files were created
            Assert.True(File.Exists(Path.Combine(uploadsDir, "42_full.webp")));
            Assert.True(File.Exists(Path.Combine(uploadsDir, "42_thumb.webp")));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(uploadsDir))
                Directory.Delete(uploadsDir, recursive: true);
        }
    }

    [Fact]
        public async Task SavePosterAsync_WithNullFile_ThrowsArgumentNullException()
        {
            var env = Mock.Of<IWebHostEnvironment>(e =>
                e.WebRootPath == Path.GetTempPath());
            var config = new ConfigurationBuilder().Build();
            var logger = Mock.Of<ILogger<PosterService>>();
            var httpClient = new HttpClient();
            var service = new PosterService(httpClient, config, env, logger);

            await Assert.ThrowsAsync<NullReferenceException>(() => service.SavePosterAsync(1, null!));
        }

    // ── GetLocalPosterPath ──────────────────────────────────────

    [Fact]
    public void GetLocalPosterPath_WhenFileExists_ReturnsPath()
    {
        var uploadsDir = Path.Combine(Path.GetTempPath(), "uploads", "posters");
        Directory.CreateDirectory(uploadsDir);
        var testFile = Path.Combine(uploadsDir, "99_thumb.webp");

        try
        {
            File.WriteAllText(testFile, "dummy");

            var env = Mock.Of<IWebHostEnvironment>(e =>
                e.WebRootPath == Path.GetTempPath());

            var config = new ConfigurationBuilder().Build();
            var logger = Mock.Of<ILogger<PosterService>>();
            var httpClient = new HttpClient();
            var service = new PosterService(httpClient, config, env, logger);

            var path = service.GetLocalPosterPath(99, "thumb");

            Assert.NotNull(path);
            Assert.Contains("uploads/posters/99_thumb.webp", path);
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    [Fact]
    public void GetLocalPosterPath_WhenFileDoesNotExist_ReturnsNull()
    {
        var env = Mock.Of<IWebHostEnvironment>(e =>
            e.WebRootPath == Path.GetTempPath());

        var config = new ConfigurationBuilder().Build();
        var logger = Mock.Of<ILogger<PosterService>>();
        var httpClient = new HttpClient();
        var service = new PosterService(httpClient, config, env, logger);

        var path = service.GetLocalPosterPath(99999, "thumb");

        Assert.Null(path);
    }
}
