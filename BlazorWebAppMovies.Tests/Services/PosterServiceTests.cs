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
        string? imageDownloadContent = null,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        if (tmdbResponse != null)
        {
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString().Contains("api.themoviedb.org/3/search")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(tmdbResponse)
                });
        }

        if (imageDownloadContent != null)
        {
            handler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString().Contains("image.tmdb.org")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(imageDownloadContent))
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

    // ── FetchAndCachePosterAsync ─────────────────────────────────

    [Fact]
    public async Task FetchAndCachePosterAsync_WithValidMovie_ReturnsFileNameAndCachesLocally()
    {
        var searchJson = JsonSerializer.Serialize(new
        {
            results = new[]
            {
                new { poster_path = "/abc123.jpg" }
            }
        });

        var (service, _) = CreateService(
            tmdbResponse: searchJson,
            imageDownloadContent: "fake-image-bytes");

        var fileName = await service.FetchAndCachePosterAsync("Mad Max", 2015, 42);

        Assert.NotNull(fileName);
        Assert.Equal("abc123.jpg", fileName);
    }

    [Fact]
    public async Task FetchAndCachePosterAsync_WithNoResults_ReturnsNull()
    {
        var json = JsonSerializer.Serialize(new
        {
            results = Array.Empty<object>()
        });

        var (service, _) = CreateService(tmdbResponse: json);
        var fileName = await service.FetchAndCachePosterAsync("NonExistentMovie", 9999, 1);

        Assert.Null(fileName);
    }

    [Fact]
    public async Task FetchAndCachePosterAsync_WithNoApiKey_ReturnsNull()
    {
        var (service, _) = CreateService(apiKey: null);
        var fileName = await service.FetchAndCachePosterAsync("Mad Max", 2015, 1);

        Assert.Null(fileName);
    }

    [Fact]
    public async Task FetchAndCachePosterAsync_WithEmptyApiKey_ReturnsNull()
    {
        var (service, _) = CreateService(apiKey: "");
        var fileName = await service.FetchAndCachePosterAsync("Mad Max", 2015, 1);

        Assert.Null(fileName);
    }

    [Fact]
    public async Task FetchAndCachePosterAsync_WithApiError_ReturnsNull()
    {
        var (service, _) = CreateService(tmdbResponse: "{}", statusCode: HttpStatusCode.InternalServerError);
        var fileName = await service.FetchAndCachePosterAsync("Mad Max", 2015, 1);

        Assert.Null(fileName);
    }

    [Fact]
    public async Task FetchAndCachePosterAsync_WithoutYear_StillWorks()
    {
        var searchJson = JsonSerializer.Serialize(new
        {
            results = new[]
            {
                new { poster_path = "/xyz.webp" }
            }
        });

        var (service, _) = CreateService(
            tmdbResponse: searchJson,
            imageDownloadContent: "fake-image-bytes");

        var fileName = await service.FetchAndCachePosterAsync("Mad Max", null, 1);

        Assert.NotNull(fileName);
        Assert.Equal("xyz.webp", fileName);
    }

    [Fact]
    public async Task FetchAndCachePosterAsync_WithNullResults_ReturnsNull()
    {
        var json = JsonSerializer.Serialize(new
        {
            results = (object?)null
        });

        var (service, _) = CreateService(tmdbResponse: json);
        var fileName = await service.FetchAndCachePosterAsync("Mad Max", 2015, 1);

        Assert.Null(fileName);
    }

    // ── SavePosterAsync ──────────────────────────────────────────

    [Fact]
    public async Task SavePosterAsync_WithValidFile_SavesAndReturnsFileName()
    {
        var uploadsDir = Path.Combine(Path.GetTempPath(), "posters", "mad-max-42");
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

            var fileMock = CreateImageFormFile("test.jpg", "image/jpeg");

            var result = await service.SavePosterAsync(42, "Mad Max", fileMock.Object);

            Assert.NotNull(result);
            Assert.EndsWith(".jpg", result);

            // Verify file was created in the movie cache directory
            var cacheDir = Path.Combine(Path.GetTempPath(), "posters", "mad-max-42");
            Assert.True(Directory.Exists(cacheDir));
            Assert.NotEmpty(Directory.GetFiles(cacheDir));
        }
        finally
        {
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

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.SavePosterAsync(1, "Test", null!));
    }

    [Fact]
    public async Task SavePosterAsync_WithInvalidExtension_ThrowsArgumentException()
    {
        var env = Mock.Of<IWebHostEnvironment>(e =>
            e.WebRootPath == Path.GetTempPath());
        var config = new ConfigurationBuilder().Build();
        var logger = Mock.Of<ILogger<PosterService>>();
        var httpClient = new HttpClient();
        var service = new PosterService(httpClient, config, env, logger);

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("malware.exe");
        fileMock.Setup(f => f.Length).Returns(100);
        fileMock.Setup(f => f.ContentType).Returns("application/x-msdownload");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SavePosterAsync(1, "Test", fileMock.Object));
    }

    [Fact]
    public async Task SavePosterAsync_WithInvalidMimeType_ThrowsArgumentException()
    {
        var env = Mock.Of<IWebHostEnvironment>(e =>
            e.WebRootPath == Path.GetTempPath());
        var config = new ConfigurationBuilder().Build();
        var logger = Mock.Of<ILogger<PosterService>>();
        var httpClient = new HttpClient();
        var service = new PosterService(httpClient, config, env, logger);

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("image.jpg");
        fileMock.Setup(f => f.Length).Returns(100);
        fileMock.Setup(f => f.ContentType).Returns("text/html");
        // Need a valid stream to pass magic byte check
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF }));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SavePosterAsync(1, "Test", fileMock.Object));
    }

    [Fact]
    public async Task SavePosterAsync_WithOversizedFile_ThrowsArgumentException()
    {
        var env = Mock.Of<IWebHostEnvironment>(e =>
            e.WebRootPath == Path.GetTempPath());
        var config = new ConfigurationBuilder().Build();
        var logger = Mock.Of<ILogger<PosterService>>();
        var httpClient = new HttpClient();
        var service = new PosterService(httpClient, config, env, logger);

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("big.jpg");
        fileMock.Setup(f => f.Length).Returns(10 * 1024 * 1024); // 10 MB > 5 MB limit
        fileMock.Setup(f => f.ContentType).Returns("image/jpeg");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SavePosterAsync(1, "Test", fileMock.Object));
    }

    // ── ResolvePosterUrl ─────────────────────────────────────────

    [Fact]
    public void ResolvePosterUrl_WithNullPosterName_ReturnsNull()
    {
        var env = Mock.Of<IWebHostEnvironment>(e =>
            e.WebRootPath == Path.GetTempPath());
        var config = new ConfigurationBuilder().Build();
        var logger = Mock.Of<ILogger<PosterService>>();
        var httpClient = new HttpClient();
        var service = new PosterService(httpClient, config, env, logger);

        var url = service.ResolvePosterUrl(null, "Mad Max", 1);

        Assert.Null(url);
    }

    [Fact]
    public void ResolvePosterUrl_WithEmptyPosterName_ReturnsNull()
    {
        var env = Mock.Of<IWebHostEnvironment>(e =>
            e.WebRootPath == Path.GetTempPath());
        var config = new ConfigurationBuilder().Build();
        var logger = Mock.Of<ILogger<PosterService>>();
        var httpClient = new HttpClient();
        var service = new PosterService(httpClient, config, env, logger);

        var url = service.ResolvePosterUrl("", "Mad Max", 1);

        Assert.Null(url);
    }

    [Fact]
    public void ResolvePosterUrl_WhenLocalFileExists_ReturnsLocalPath()
    {
        var tempRoot = Path.GetTempPath();
        var slug = "mad-max";
        var cacheDir = Path.Combine(tempRoot, "posters", $"{slug}-42");
        Directory.CreateDirectory(cacheDir);
        var testFile = Path.Combine(cacheDir, "abc123.jpg");
        File.WriteAllText(testFile, "dummy");

        try
        {
            var env = Mock.Of<IWebHostEnvironment>(e =>
                e.WebRootPath == tempRoot);
            var config = new ConfigurationBuilder().Build();
            var logger = Mock.Of<ILogger<PosterService>>();
            var httpClient = new HttpClient();
            var service = new PosterService(httpClient, config, env, logger);

            var url = service.ResolvePosterUrl("abc123.jpg", "Mad Max", 42);

            Assert.NotNull(url);
            Assert.StartsWith("/", url);
            Assert.Contains("mad-max-42", url);
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
    }

    [Fact]
    public void ResolvePosterUrl_WhenLocalFileNotExists_ReturnsTmdbFallback()
    {
        var env = Mock.Of<IWebHostEnvironment>(e =>
            e.WebRootPath == Path.GetTempPath());
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tmdb:ImageBaseUrl"] = "https://image.tmdb.org/t/p/"
            })
            .Build();
        var logger = Mock.Of<ILogger<PosterService>>();
        var httpClient = new HttpClient();
        var service = new PosterService(httpClient, config, env, logger);

        var url = service.ResolvePosterUrl("abc123.jpg", "Mad Max", 999);

        Assert.NotNull(url);
        Assert.StartsWith("https://image.tmdb.org", url);
        Assert.Contains("abc123.jpg", url);
    }

    // ── GetMovieSlug ─────────────────────────────────────────────

    [Fact]
    public void GetMovieSlug_WithNormalTitle_ReturnsSlug()
    {
        var service = CreateService().Service;
        Assert.Equal("mad-max-fury-road", service.GetMovieSlug("Mad Max: Fury Road"));
    }

    [Fact]
    public void GetMovieSlug_WithSpecialCharacters_ReturnsCleanSlug()
    {
        var service = CreateService().Service;
        Assert.Equal("the-terminator-2", service.GetMovieSlug("The Terminator 2! @#$%"));
    }

    [Fact]
    public void GetMovieSlug_WithNullTitle_ReturnsUntitled()
    {
        var service = CreateService().Service;
        Assert.Equal("untitled", service.GetMovieSlug(null!));
    }

    [Fact]
    public void GetMovieSlug_WithEmptyTitle_ReturnsUntitled()
    {
        var service = CreateService().Service;
        Assert.Equal("untitled", service.GetMovieSlug(""));
    }

    // ── GetLocalPosterPath ───────────────────────────────────────

    [Fact]
    public void GetLocalPosterPath_WhenFileExists_ReturnsPath()
    {
        var tempRoot = Path.GetTempPath();
        var slug = "the-matrix";
        var cacheDir = Path.Combine(tempRoot, "posters", $"{slug}-99");
        Directory.CreateDirectory(cacheDir);
        var testFile = Path.Combine(cacheDir, "poster.webp");
        File.WriteAllText(testFile, "dummy");

        try
        {
            var env = Mock.Of<IWebHostEnvironment>(e =>
                e.WebRootPath == tempRoot);
            var config = new ConfigurationBuilder().Build();
            var logger = Mock.Of<ILogger<PosterService>>();
            var httpClient = new HttpClient();
            var service = new PosterService(httpClient, config, env, logger);

            var path = service.GetLocalPosterPath(99, "The Matrix", "poster.webp");

            Assert.NotNull(path);
            Assert.Contains("posters/the-matrix-99/poster.webp", path);
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
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

        var path = service.GetLocalPosterPath(99999, "NonExistent", "nope.jpg");

        Assert.Null(path);
    }

    // ── DeleteLocalPoster ────────────────────────────────────────

    [Fact]
    public void DeleteLocalPoster_RemovesCacheDirectory()
    {
        var tempRoot = Path.GetTempPath();
        var cacheDir = Path.Combine(tempRoot, "posters", "delete-test-1");
        Directory.CreateDirectory(cacheDir);
        File.WriteAllText(Path.Combine(cacheDir, "poster.jpg"), "dummy");

        try
        {
            var env = Mock.Of<IWebHostEnvironment>(e =>
                e.WebRootPath == tempRoot);
            var config = new ConfigurationBuilder().Build();
            var logger = Mock.Of<ILogger<PosterService>>();
            var httpClient = new HttpClient();
            var service = new PosterService(httpClient, config, env, logger);

            service.DeleteLocalPoster(1, "Delete Test", "poster.jpg");

            Assert.False(Directory.Exists(cacheDir));
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
    }

    // ── Helper ───────────────────────────────────────────────────

    private static Mock<IFormFile> CreateImageFormFile(string fileName, string contentType)
    {
        var fileMock = new Mock<IFormFile>();

        // JPEG magic bytes + some data
        var content = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        var ms = new MemoryStream(content);

        fileMock.Setup(f => f.OpenReadStream()).Returns(ms);
        fileMock.Setup(f => f.Length).Returns(ms.Length);
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.ContentType).Returns(contentType);

        return fileMock;
    }
}
