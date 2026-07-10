using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BlazorWebAppMovies.Data;
using BlazorWebAppMovies.Models;
using BlazorWebAppMovies.Models.Dtos;
using BlazorWebAppMovies.Services;

namespace BlazorWebAppMovies.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MoviesController : ControllerBase
{
    private readonly IDbContextFactory<BlazorWebAppMoviesContext> _contextFactory;
    private readonly IMapper _mapper;
    private readonly IPosterService _posterService;

    public MoviesController(
        IDbContextFactory<BlazorWebAppMoviesContext> contextFactory,
        IMapper mapper,
        IPosterService posterService)
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
        _posterService = posterService;
    }

    /// <summary>
    /// GET /api/movies — returns all movies. Public access.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<MovieDto>>> GetAll()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var movies = await context.Movie
            .Include(m => m.MovieRating)
            .OrderBy(m => m.ReleaseDate)
            .ToListAsync();

        var dtos = _mapper.Map<List<MovieDto>>(movies);
        ResolvePosterUrls(dtos);
        return Ok(dtos);
    }

    /// <summary>
    /// GET /api/movies/{id} — returns a single movie. Public access.
    /// </summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<MovieDto>> GetById(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var movie = await context.Movie
            .Include(m => m.MovieRating)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (movie == null)
            return NotFound(new { Message = $"Movie with Id {id} not found." });

        var dto = _mapper.Map<MovieDto>(movie);
        ResolvePosterUrl(dto);
        return Ok(dto);
    }

    /// <summary>
    /// POST /api/movies — creates a new movie.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<MovieDto>> Create([FromBody] CreateMovieDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        await using var context = await _contextFactory.CreateDbContextAsync();

        // Check for duplicate title (case-insensitive, trimmed)
        var title = dto.Title.Trim();
        if (await context.Movie.AnyAsync(m => m.Title!.ToLower() == title.ToLower()))
            return Conflict(new { Message = $"A movie with title '{title}' already exists." });

        // Resolve the rating code to a foreign key
        var rating = await context.MovieRating.FirstOrDefaultAsync(r => r.Code == dto.Rating);
        if (rating == null)
            return BadRequest(new { Message = $"Invalid rating '{dto.Rating}'." });

        var movie = _mapper.Map<Movie>(dto);
        movie.Title = title;
        movie.MovieRatingId = rating.Id;

        context.Movie.Add(movie);
        await context.SaveChangesAsync();

        // Auto-fetch poster from TMDB after creation
        var posterName = await _posterService.FetchAndCachePosterAsync(
            movie.Title, movie.ReleaseDate.Year, movie.Id);
        if (posterName != null)
        {
            movie.PosterUrl = posterName;
            await context.SaveChangesAsync();
        }

        var result = _mapper.Map<MovieDto>(movie);
        ResolvePosterUrl(result);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// PUT /api/movies/{id} — updates an existing movie.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<MovieDto>> Update(int id, [FromBody] UpdateMovieDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var movie = await context.Movie.FindAsync(id);

        if (movie == null)
            return NotFound(new { Message = $"Movie with Id {id} not found." });

        // Check for duplicate title, excluding the current movie
        var title = dto.Title.Trim();
        if (await context.Movie.AnyAsync(m => m.Title!.ToLower() == title.ToLower() && m.Id != id))
            return Conflict(new { Message = $"A movie with title '{title}' already exists." });

        // Resolve the rating code to a foreign key
        var rating = await context.MovieRating.FirstOrDefaultAsync(r => r.Code == dto.Rating);
        if (rating == null)
            return BadRequest(new { Message = $"Invalid rating '{dto.Rating}'." });

        // AutoMapper maps the DTO onto the existing entity, preserving the Id
        _mapper.Map(dto, movie);
        movie.Title = title;
        movie.MovieRatingId = rating.Id;

        // Reload the rating navigation for the response DTO
        await context.Entry(movie).Reference(m => m.MovieRating).LoadAsync();
        await context.SaveChangesAsync();

        var result = _mapper.Map<MovieDto>(movie);
        ResolvePosterUrl(result);
        return Ok(result);
    }

    /// <summary>
    /// DELETE /api/movies/{id} — deletes a movie and its local poster files.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var movie = await context.Movie.FindAsync(id);

        if (movie == null)
            return NotFound(new { Message = $"Movie with Id {id} not found." });

        // Clean up local poster files
        _posterService.DeleteLocalPoster(id, movie.Title!, movie.PosterUrl);

        context.Movie.Remove(movie);
        await context.SaveChangesAsync();

        return NoContent();
    }

    // ── Poster endpoints ──

    /// <summary>
    /// POST /api/movies/poster/backfill — fetch posters for all movies with null PosterUrl.
    /// </summary>
    [HttpPost("poster/backfill")]
    [AllowAnonymous]
    public async Task<ActionResult<BackfillResult>> BackfillPosters()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var movies = await context.Movie
            .Where(m => m.PosterUrl == null)
            .ToListAsync();

        if (movies.Count == 0)
        {
            return Ok(new BackfillResult
            {
                Total = 0,
                Succeeded = 0,
                Failed = 0
            });
        }

        var succeeded = 0;
        var failed = 0;
        var errors = new List<string>();

        foreach (var movie in movies)
        {
            try
            {
                var posterName = await _posterService.FetchAndCachePosterAsync(
                    movie.Title!, movie.ReleaseDate.Year, movie.Id);
                if (posterName != null)
                {
                    movie.PosterUrl = posterName;
                    succeeded++;
                }
                else
                {
                    failed++;
                }
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"{movie.Title}: {ex.Message}");
            }
        }

        await context.SaveChangesAsync();

        return Ok(new BackfillResult
        {
            Total = movies.Count,
            Succeeded = succeeded,
            Failed = failed,
            Errors = errors
        });
    }

    /// <summary>
    /// POST /api/movies/{id}/poster/fetch — auto-download poster from TMDB by movie title.
    /// </summary>
    [HttpPost("{id:int}/poster/fetch")]
    public async Task<ActionResult<MovieDto>> FetchPoster(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var movie = await context.Movie
            .Include(m => m.MovieRating)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (movie == null)
            return NotFound(new { Message = $"Movie with Id {id} not found." });

        var posterName = await _posterService.FetchAndCachePosterAsync(
            movie.Title!, movie.ReleaseDate.Year, movie.Id);
        if (posterName == null)
            return NotFound(new { Message = "No poster found for this movie." });

        movie.PosterUrl = posterName;
        await context.SaveChangesAsync();

        var dto = _mapper.Map<MovieDto>(movie);
        ResolvePosterUrl(dto);
        return Ok(dto);
    }

    /// <summary>
    /// POST /api/movies/{id}/poster/upload — manually upload a poster image.
    /// </summary>
    [HttpPost("{id:int}/poster/upload")]
    public async Task<ActionResult<MovieDto>> UploadPoster(int id, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { Message = "No file provided." });

        await using var context = await _contextFactory.CreateDbContextAsync();
        var movie = await context.Movie
            .Include(m => m.MovieRating)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (movie == null)
            return NotFound(new { Message = $"Movie with Id {id} not found." });

        string posterName;
        try
        {
            posterName = await _posterService.SavePosterAsync(id, movie.Title!, file);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }

        movie.PosterUrl = posterName;
        await context.SaveChangesAsync();

        var dto = _mapper.Map<MovieDto>(movie);
        ResolvePosterUrl(dto);
        return Ok(dto);
    }

    /// <summary>
    /// DELETE /api/movies/{id}/poster — remove the local poster and clear DB field.
    /// </summary>
    [HttpDelete("{id:int}/poster")]
    public async Task<IActionResult> DeletePoster(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var movie = await context.Movie.FindAsync(id);

        if (movie == null)
            return NotFound(new { Message = $"Movie with Id {id} not found." });

        // Delete local poster files
        _posterService.DeleteLocalPoster(id, movie.Title!, movie.PosterUrl);

        movie.PosterUrl = null;
        await context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// GET /api/movies/{id}/poster — returns the poster image file if local.
    /// </summary>
    [HttpGet("{id:int}/poster")]
    [AllowAnonymous]
    public IActionResult GetPoster(int id)
    {
        // We need the movie title to resolve the local path.
        // Use a light-weight approach: try known poster names or glob the directory.
        // Since the DB stores the filename, we fetch the movie.
        using var context = _contextFactory.CreateDbContext();
        var movie = context.Movie.Find(id);
        if (movie == null)
            return NotFound();

        var localPath = _posterService.GetLocalPosterPath(id, movie.Title!, movie.PosterUrl);
        if (localPath == null)
            return NotFound();

        var filePath = Path.Combine("wwwroot", localPath.TrimStart('/'));
        if (!System.IO.File.Exists(filePath))
            return NotFound();

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var contentType = extension switch
        {
            ".webp" => "image/webp",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };

        return PhysicalFile(filePath, contentType);
    }

    // ── Private helpers ──

    /// <summary>
    /// Resolves the PosterUrl (just a filename) to a full resolvable URL
    /// on a single MovieDto.
    /// </summary>
    private void ResolvePosterUrl(MovieDto dto)
    {
        dto.PosterUrl = _posterService.ResolvePosterUrl(
            dto.PosterUrl, dto.Title, dto.Id, "w500");
    }

    /// <summary>
    /// Resolves PosterUrl on every DTO in the list.
    /// </summary>
    private void ResolvePosterUrls(List<MovieDto> dtos)
    {
        foreach (var dto in dtos)
        {
            ResolvePosterUrl(dto);
        }
    }
}
