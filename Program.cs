using System.Diagnostics;
using System.Text;
using BlazorWebAppMovies.Components;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using BlazorWebAppMovies.Data;
using AutoMapper;
using BlazorWebAppMovies.Models;
using BlazorWebAppMovies.Models.Mapping;
using BlazorWebAppMovies.Components.Handlers;
using BlazorWebAppMovies.Services;
using BlazorWebAppMovies;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.OpenApi.Models;




// ── Suppress noisy EF Core SQL logs ──
AppContext.SetSwitch("Microsoft.EntityFrameworkCore.Issue21997", true);

var builder = WebApplication.CreateBuilder(args);

var dbProvider = DbContextProvider.Create(builder.Configuration);

// Register the base factory (for Identity/seed) and the concrete factory (for migrations)
builder.Services.AddDbContextFactory<BlazorWebAppMoviesContext>(dbProvider.ConfigureDbContext);
dbProvider.RegisterConcreteFactory(builder.Services);

builder.Services.AddIdentity<User, IdentityRole>()
    .AddEntityFrameworkStores<BlazorWebAppMoviesContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            RoleClaimType = "role"
        };

        // Also read JWT from cookie for Blazor Server SignalR circuit
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Cookies["auth_token"];
                if (!string.IsNullOrEmpty(token))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "BlazorWebAppMovies API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },[]
        }
    });

    // Use meaningful examples instead of Swagger's random generation
    options.SchemaFilter<SwaggerExampleFilter>();

    // Describe how to get JWT from login for Swagger Authorize
    options.OperationFilter<SwaggerLoginDescriptionFilter>();

});
builder.Services.AddSingleton(sp =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var config = new MapperConfiguration(cfg => cfg.AddProfile<MovieProfile>(), loggerFactory);
    config.AssertConfigurationIsValid();
    return config.CreateMapper();
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<AuthCookieHandler>();
builder.Services.AddHttpClient("BlazorApi", client =>
{
    // Set a placeholder base so SendAsync validation passes for relative URIs.
    // The AuthCookieHandler will replace it with the real host per-request.
    client.BaseAddress = new Uri("http://localhost");
})
    .AddHttpMessageHandler<AuthCookieHandler>();

// Register HTTP client for PosterService (TMDB API)
builder.Services.AddHttpClient<IPosterService, PosterService>(client =>
{
    client.BaseAddress = new Uri("https://api.themoviedb.org");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddQuickGridEntityFrameworkAdapter();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── Suppress EF Core SQL logs in console ──
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Query", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Infrastructure", LogLevel.Warning);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var baseFactory = services.GetRequiredService<IDbContextFactory<BlazorWebAppMoviesContext>>();

    using var migrationContext = dbProvider.CreateMigrationContext(services);
    // Check for pending model changes
    var migrator = migrationContext.Database.GetService<IMigrator>();
    if (migrator.HasPendingModelChanges())
    {
        Console.WriteLine("");
        Console.WriteLine("⚠️  ╔══════════════════════════════════════════════════╗");
        Console.WriteLine("⚠️  ║  Model has pending changes!                      ║");
        Console.WriteLine("⚠️  ║  Run: dotnet ef migrations add <description>     ║");
        Console.WriteLine("⚠️  ╚══════════════════════════════════════════════════╝");
        Console.WriteLine("");
    }
    migrationContext.Database.Migrate();

    if (app.Environment.IsDevelopment())
    {
        SeedData.Initialize(baseFactory, services).GetAwaiter().GetResult();
    }
}


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseMigrationsEndPoint();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();
app.UseStaticFiles();

// ── Request logging middleware (after auth so User is populated) ──

var api = new PathString("/api");
var classicApi = new PathString("/classic");

app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var method = context.Request.Method;
    var identity = context.User.Identity;


    bool isAuthenticated = identity?.IsAuthenticated == true;
    var auth = isAuthenticated ? "authenticated" : "anonymous";
    var user = isAuthenticated ? (identity?.Name ?? "(none)") : "(none)";
    if (path.StartsWithSegments(api) || path.StartsWithSegments(classicApi))
    {
        Console.WriteLine($"[REQ] {method} {path} - {auth}, user: {user}");
        var stopwatch = Stopwatch.StartNew();
        await next();
        stopwatch.Stop();
        var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
        Console.WriteLine($"[RES] {method} {path} - {context.Response.StatusCode} {elapsedMs:F0}ms");
    }
    else
    {
        await next();
    }
});

app.MapStaticAssets();
app.MapRazorPages();
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
