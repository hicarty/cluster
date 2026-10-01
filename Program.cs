using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Cluster.Identity;
using Cluster.Identity.Data;
using Cluster.Identity.Endpoints;
using Cluster.Identity.Shared;

const string IdentityCookieName = ".AspNetCore.Identity.Application";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();

// Persist the key ring outside the project so Cluster.View can read the cookies issued here.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(SharedKeyRing.Resolve(builder.Environment.ContentRootPath)))
    .SetApplicationName("Cluster.Identity");

builder.Services.Configure<ViewClientOptions>(
    builder.Configuration.GetSection(ViewClientOptions.SectionName));

// The login and sign-out forms change the signed-in state, so they use a double-submit form token
// rather than the framework antiforgery service, whose tokens are bound to the current user.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<FormTokenService>();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        // The scaffolded email sender is a no-op, so requiring a confirmed account would lock
        // every new user out of the demo.
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 8;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

// Cluster.View decrypts this cookie, so the name and expiry have to stay in step with its configuration.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = IdentityCookieName;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Apply migrations on start so the app is usable straight from a terminal, without needing the
// dotnet-ef global tool installed.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapIdentityEndpoints();

app.Run();

/// <summary>Request payloads accepted by the identity endpoints.</summary>
public sealed class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public bool RememberMe { get; set; }
}

public sealed class RegisterRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    [StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";
}

public sealed record IdentityResponse(string Email, string UserId, string[] Roles);