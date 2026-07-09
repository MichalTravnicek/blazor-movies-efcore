namespace BlazorWebAppMovies.Models.Dtos;

/// <summary>
/// Result of a batch poster backfill operation.
/// </summary>
public class BackfillResult
{
    public int Total { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = new();
}
