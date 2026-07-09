using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BlazorWebAppMovies.Data;
using BlazorWebAppMovies.Models;
using BlazorWebAppMovies.Models.Dtos;

namespace BlazorWebAppMovies.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MoviesController : ControllerBase
{
    private readonly IDbContextFactory<BlazorWebAppMoviesContext> _contextFactory;
    private readonly IMapper _mapper;

    public MoviesController(IDbContextFactory<BlazorWebAppMoviesContext> contextFactory, IMapper mapper)
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
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

        return Ok(_mapper.Map<List<MovieDto>>(movies));
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

        return Ok(_mapper.Map<MovieDto>(movie));
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

        var result = _mapper.Map<MovieDto>(movie);
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

        return Ok(_mapper.Map<MovieDto>(movie));
    }

    /// <summary>
    /// DELETE /api/movies/{id} — deletes a movie.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var movie = await context.Movie.FindAsync(id);

        if (movie == null)
            return NotFound(new { Message = $"Movie with Id {id} not found." });

        context.Movie.Remove(movie);
        await context.SaveChangesAsync();

        return NoContent();
    }
}
