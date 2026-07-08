using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BlazorWebAppMovies;

/// <summary>
/// Provides realistic example values for DTOs in Swagger UI,
/// replacing the auto-generated random strings.
/// </summary>
public class SwaggerExampleFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(Models.Dtos.CreateMovieDto))
        {
            schema.Example = new Microsoft.OpenApi.Any.OpenApiObject
            {
                ["title"] = new Microsoft.OpenApi.Any.OpenApiString("Inception"),
                ["genre"] = new Microsoft.OpenApi.Any.OpenApiString("Sci-Fi"),
                ["price"] = new Microsoft.OpenApi.Any.OpenApiDouble(12.99),
                ["releaseDate"] = new Microsoft.OpenApi.Any.OpenApiString("2010-07-16"),
                ["rating"] = new Microsoft.OpenApi.Any.OpenApiString("PG-13")
            };
        }
        else if (context.Type == typeof(Models.Dtos.UpdateMovieDto))
        {
            schema.Example = new Microsoft.OpenApi.Any.OpenApiObject
            {
                ["title"] = new Microsoft.OpenApi.Any.OpenApiString("The Dark Knight"),
                ["genre"] = new Microsoft.OpenApi.Any.OpenApiString("Action"),
                ["price"] = new Microsoft.OpenApi.Any.OpenApiDouble(14.99),
                ["releaseDate"] = new Microsoft.OpenApi.Any.OpenApiString("2008-07-18"),
                ["rating"] = new Microsoft.OpenApi.Any.OpenApiString("PG-13")
            };
        }
        else if (context.Type == typeof(Models.Dtos.MovieDto))
        {
            schema.Example = new Microsoft.OpenApi.Any.OpenApiObject
            {
                ["id"] = new Microsoft.OpenApi.Any.OpenApiInteger(1),
                ["title"] = new Microsoft.OpenApi.Any.OpenApiString("Inception"),
                ["genre"] = new Microsoft.OpenApi.Any.OpenApiString("Sci-Fi"),
                ["price"] = new Microsoft.OpenApi.Any.OpenApiDouble(12.99),
                ["releaseDate"] = new Microsoft.OpenApi.Any.OpenApiString("2010-07-16"),
                ["rating"] = new Microsoft.OpenApi.Any.OpenApiString("PG-13")
            };
        }
        else if (context.Type == typeof(Controllers.AuthController.LoginRequest))
        {
            schema.Example = new Microsoft.OpenApi.Any.OpenApiObject
            {
                ["email"] = new Microsoft.OpenApi.Any.OpenApiString("admin@example.com"),
                ["password"] = new Microsoft.OpenApi.Any.OpenApiString("Admin123!")
            };
        }
    }
}

/// <summary>
/// Adds a description to the login endpoint explaining how to get the JWT for Swagger authorization.
/// </summary>
public class SwaggerLoginDescriptionFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType == typeof(Controllers.AuthController) &&
            context.MethodInfo.Name == "Login")
        {
            operation.Description = "Login returns the JWT in an HttpOnly cookie (not in the response body). " +
                "To get the token for Swagger Authorization, open your browser's DevTools (F12) → Network tab, " +
                "execute this request, find the login response, and copy the value from the " +
                "`Set-Cookie: auth_token=<token>` response header. Then click the Authorize button and paste it." +
                "But - for trying authenticated endpoints it is sufficient to be logged in using Web UI";
        }
    }
}
