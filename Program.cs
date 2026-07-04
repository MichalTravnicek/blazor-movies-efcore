using BlazorWebAppMovies.Components;
using Microsoft.EntityFrameworkCore;
using BlazorWebAppMovies.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("BlazorWebAppMoviesContext")
                       ?? throw new InvalidOperationException("Connection string 'BlazorWebAppMoviesContext' not found.");
var databaseProvider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";

builder.Services.AddDbContextFactory<BlazorWebAppMoviesContext>(options =>
{
    if (databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        var sqlServerConnectionString = builder.Configuration.GetConnectionString("BlazorWebAppMoviesContextSqlServer")
                                        ?? throw new InvalidOperationException("Connection string 'BlazorWebAppMoviesContextSqlServer' not found.");
        options.UseSqlServer(sqlServerConnectionString);
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

builder.Services.AddQuickGridEntityFrameworkAdapter();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    SeedData.Initialize(services);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseMigrationsEndPoint();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();