using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlazorWebAppMovies.Pages.Classic.Movies;

[AllowAnonymous]
public class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}
