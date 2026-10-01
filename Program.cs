using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Cluster.View;
using Cluster.View.Components;

// Add services to the container.
const string IdentityCookieName = ".AspNetCore.Identity.Application";

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<IdentityClientOptions>(
    builder.Configuration.GetSection(IdentityClientOptions.SectionName));

// Share the data protection key ring with Cluster.Identity so this app can decrypt the
// authentication cookie issued there. The application name and cookie scheme must match,
// otherwise the ticket cannot be unprotected.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(SharedKeyRing.Resolve(builder.Environment.ContentRootPath)))
    .SetApplicationName("Cluster.Identity");

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
    })
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        // Both the cookie name and the scheme name have to match AddIdentityCookies in
        // Cluster.Identity, otherwise the shared key ring cannot unprotect the ticket.
        options.Cookie.Name = IdentityCookieName;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Sign-out has to delete the cookie Cluster.Identity set, so it is performed here rather than
// round-tripping to the identity app. Cluster.Identity exposes the same endpoint for its own pages.
// The antiforgery token is validated explicitly, since a formless endpoint is not inferred as
// requiring one.
app.MapPost("/account/logout", async (
    HttpContext context,
    IAntiforgery antiforgery,
    IOptions<IdentityClientOptions> identityClient) =>
{
    try
    {
        await antiforgery.ValidateRequestAsync(context);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.BadRequest("The antiforgery token was missing or invalid.");
    }

    await context.SignOutAsync(IdentityConstants.ApplicationScheme);

    return Results.Redirect(identityClient.Value.ViewBaseUrl.TrimEnd('/') + "/");
});

app.Run();