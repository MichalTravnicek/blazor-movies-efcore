using System.Text;
using BlazorWebAppMovies.Components;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using BlazorWebAppMovies.Data;
using BlazorWebAppMovies.Models;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("BlazorWebAppMoviesContext")
                       ?? throw new InvalidOperationException("Connection string 'BlazorWebAppMoviesContext' not found.");
var databaseProvider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";

if (databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
{
    var sqlServerConnectionString = builder.Configuration.GetConnectionString("BlazorWebAppMoviesContextSqlServer")
                                    ?? throw new InvalidOperationException("Connection string 'BlazorWebAppMoviesContextSqlServer' not found.");

    builder.Services.AddDbContextFactory<BlazorWebAppMoviesContext>(options =>
        options.UseSqlServer(sqlServerConnectionString));
}
else
{
    builder.Services.AddDbContextFactory<BlazorWebAppMoviesContext>(options =>
        options.UseSqlite(connectionString));
}

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
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();

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
            },
            []
        }
    });
});
builder.Services.AddQuickGridEntityFrameworkAdapter();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider
        .GetRequiredService<IDbContextFactory<BlazorWebAppMoviesContext>>();
    using var context = factory.CreateDbContext();
    context.Database.Migrate();

    SeedData.Initialize(factory);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseMigrationsEndPoint();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
