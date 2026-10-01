using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Cluster.Identity;
using Cluster.Identity.Data;
using Cluster.Identity.Shared;

namespace Cluster.Identity.Endpoints;

/// <summary>
/// Owns the whole authentication journey: the login and register forms, credential validation,
/// cookie issuance and sign-out. There is no Blazor here on purpose, so this project stays
/// runnable and debuggable from a terminal with curl.
/// </summary>
public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        // These forms carry their own double-submit token, see FormTokenService.
        app.MapGet("/identity/login", LoginForm);
        app.MapPost("/identity/login", HandleLoginAsync).DisableAntiforgery();
        app.MapGet("/identity/register", RegisterForm);
        app.MapPost("/identity/register", HandleRegisterAsync).DisableAntiforgery();
        app.MapPost("/identity/logout", HandleLogoutAsync).DisableAntiforgery();
        app.MapGet("/identity/logged-out", LoggedOut);

        // JSON endpoints, handy for scripted checks and non-browser callers.
        app.MapPost("/api/identity/login", LoginJsonAsync);
        app.MapPost("/api/identity/register", RegisterJsonAsync);
        app.MapPost("/api/identity/logout", LogoutJsonAsync);
        app.MapGet("/api/identity/me", Me);

        return app;
    }

    private static IResult LoginForm(FormTokenService formTokens, [FromQuery] string? returnUrl)
        => Results.Content(
            Html.Page(
                title: "Log in",
                heading: "Log in",
                body: LoginFormMarkup(formTokens.Issue(), returnUrl, error: null)),
            "text/html");

    private static async Task<IResult> HandleLoginAsync(
        FormTokenService formTokens,
        SignInManager<ApplicationUser> signInManager,
        IOptions<ViewClientOptions> viewClient,
        [FromForm] string email,
        [FromForm] string password,
        [FromForm] bool? rememberMe,
        [FromForm] string? returnUrl,
        [FromForm(Name = "__FormToken")] string? formToken)
    {
        if (!formTokens.Validate(formToken))
        {
            return FormExpired("Log in", LoginFormMarkup(formTokens.Issue(), returnUrl, error: null));
        }

        var result = await signInManager.PasswordSignInAsync(
            email, password, rememberMe ?? false, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            return RedirectToClient(viewClient.Value, returnUrl)
                ?? Results.Redirect("/identity/logged-out");
        }

        var (error, status) = result switch
        {
            { IsLockedOut: true } => ("This account is locked out.", HttpStatusCode.Locked),
            { RequiresTwoFactor: true } => ("This account requires two factor authentication.", HttpStatusCode.Unauthorized),
            _ => ("Invalid login attempt.", HttpStatusCode.Unauthorized),
        };

        return Results.Content(
            Html.Page(
                title: "Log in",
                heading: "Log in",
                body: LoginFormMarkup(formTokens.Issue(), returnUrl, error)),
            "text/html",
            statusCode: (int)status);
    }

    private static IResult RegisterForm(FormTokenService formTokens, [FromQuery] string? returnUrl)
        => Results.Content(
            Html.Page(
                title: "Register",
                heading: "Register",
                body: RegisterFormMarkup(formTokens.Issue(), returnUrl, error: null)),
            "text/html");

    private static async Task<IResult> HandleRegisterAsync(
        FormTokenService formTokens,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOptions<ViewClientOptions> viewClient,
        [FromForm] string email,
        [FromForm] string password,
        [FromForm] string confirmPassword,
        [FromForm] string? returnUrl,
        [FromForm(Name = "__FormToken")] string? formToken)
    {
        if (!formTokens.Validate(formToken))
        {
            return FormExpired("Register", RegisterFormMarkup(formTokens.Issue(), returnUrl, error: null));
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            return RegistrationError(
                returnUrl,
                "The password and confirmation password do not match.",
                formTokens.Issue());
        }

        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            return RegistrationError(
                returnUrl,
                string.Join(" ", result.Errors.Select(e => e.Description)),
                formTokens.Issue());
        }

        await signInManager.SignInAsync(user, isPersistent: false);

        return RedirectToClient(viewClient.Value, returnUrl)
            ?? Results.Redirect("/identity/logged-out");
    }

    private static async Task<IResult> HandleLogoutAsync(
        FormTokenService formTokens,
        HttpContext context,
        IOptions<ViewClientOptions> viewClient,
        [FromForm] string? returnUrl,
        [FromForm(Name = "__FormToken")] string? formToken)
    {
        if (!formTokens.Validate(formToken))
        {
            return FormExpired("Log out", $"<p class=\"error\">The form expired. Please try again.</p>");
        }

        await context.SignOutAsync(IdentityConstants.ApplicationScheme);

        return RedirectToClient(viewClient.Value, returnUrl)
            ?? Results.Redirect("/identity/logged-out");
    }

    private static IResult LoggedOut(IOptions<ViewClientOptions> viewClient, [FromQuery] string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && IsAllowedReturnUrl(viewClient.Value, returnUrl))
        {
            return Results.Content(
                Html.Page(
                    title: "Signed out",
                    heading: "Signed out",
                    body: $"""
                        <p class="ok">You have been signed out.</p>
                        <p><a href="{HtmlEncoder.Default.Encode(returnUrl!)}">Back to Cluster.View</a></p>
                        """),
                "text/html");
        }

        var body = "<p class=\"ok\">You have been signed out.</p>";

        return Results.Content(
            Html.Page(title: "Signed out", heading: "Signed out", body: body),
            "text/html");
    }

    private static async Task<IResult> LoginJsonAsync(
        SignInManager<ApplicationUser> signInManager,
        LoginRequest request)
    {
        var result = await signInManager.PasswordSignInAsync(
            request.Email, request.Password, request.RememberMe, lockoutOnFailure: false);

        if (result.IsLockedOut)
        {
            return Results.Problem(
                title: "Account locked out",
                statusCode: (int)HttpStatusCode.Locked);
        }

        if (!result.Succeeded)
        {
            return Results.Problem(
                title: "Invalid login attempt",
                detail: result.RequiresTwoFactor
                    ? "This account requires two factor authentication, which the JSON API does not expose yet."
                    : null,
                statusCode: (int)HttpStatusCode.Unauthorized);
        }

        var user = await signInManager.UserManager.FindByEmailAsync(request.Email)
            ?? throw new InvalidOperationException($"'{request.Email}' signed in but could not be loaded.");

        return Results.Ok(new IdentityResponse(user.Email ?? request.Email, user.Id, Array.Empty<string>()));
    }

    private static async Task<IResult> RegisterJsonAsync(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RegisterRequest request)
    {
        var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return Results.ValidationProblem(result.Errors.ToDictionary(
                error => error.Code,
                error => new[] { error.Description }));
        }

        await signInManager.SignInAsync(user, isPersistent: false);

        return Results.Ok(new IdentityResponse(user.Email ?? request.Email, user.Id, Array.Empty<string>()));
    }

    private static async Task<IResult> LogoutJsonAsync(HttpContext context, CancellationToken cancellationToken)
    {
        await context.SignOutAsync(IdentityConstants.ApplicationScheme);
        return Results.NoContent();
    }

    private static IResult Me(HttpContext context)
        => context.User.Identity?.IsAuthenticated == true
            ? Results.Ok(ToResponse(context.User))
            : Results.Unauthorized();

    private static IResult RegistrationError(string? returnUrl, string error, string formToken)
        => Results.Content(
            Html.Page(title: "Register", heading: "Register", body: RegisterFormMarkup(formToken, returnUrl, error)),
            "text/html",
            statusCode: (int)HttpStatusCode.BadRequest);

    private static IResult FormExpired(string heading, string body)
        => Results.Content(
            Html.Page(title: heading, heading: heading, body: body),
            "text/html",
            statusCode: (int)HttpStatusCode.BadRequest);

    private static string LoginFormMarkup(string formToken, string? returnUrl, string? error) => $$"""
        {{ErrorMarkup(error)}}
        <form method="post" action="/identity/login">
            {{HiddenFields(formToken, returnUrl)}}
            <label for="email">Email</label>
            <input id="email" name="email" type="email" autocomplete="username" required />
            <label for="password">Password</label>
            <input id="password" name="password" type="password" autocomplete="current-password" required />
            <label class="check">
                <input name="rememberMe" type="checkbox" value="true" />
                Remember me
            </label>
            <button type="submit">Log in</button>
        </form>
        <p class="alt">No account yet? <a href="/identity/register{{Query(returnUrl)}}">Register</a></p>
        """;

    private static string RegisterFormMarkup(string formToken, string? returnUrl, string? error) => $$"""
        {{ErrorMarkup(error)}}
        <form method="post" action="/identity/register">
            {{HiddenFields(formToken, returnUrl)}}
            <label for="email">Email</label>
            <input id="email" name="email" type="email" autocomplete="username" required />
            <label for="password">Password</label>
            <input id="password" name="password" type="password" autocomplete="new-password" minlength="8" required />
            <label for="confirmPassword">Confirm password</label>
            <input id="confirmPassword" name="confirmPassword" type="password" autocomplete="new-password" minlength="8" required />
            <button type="submit">Register</button>
        </form>
        <p class="alt">Already registered? <a href="/identity/login{{Query(returnUrl)}}">Log in</a></p>
        """;

    private static string ErrorMarkup(string? error)
        => error is null ? string.Empty : $"<p class=\"error\">{HtmlEncoder.Default.Encode(error)}</p>";

    private static string HiddenFields(string formToken, string? returnUrl)
        => $"""
            <input type="hidden" name="returnUrl" value="{HtmlEncoder.Default.Encode(returnUrl ?? string.Empty)}" />
            <input type="hidden" name="__FormToken" value="{HtmlEncoder.Default.Encode(formToken)}" />
            """;

    private static string Query(string? returnUrl)
        => string.IsNullOrEmpty(returnUrl)
            ? string.Empty
            : $"?returnUrl={Uri.EscapeDataString(returnUrl)}";

    private static IResult? RedirectToClient(ViewClientOptions viewClient, string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl) || !IsAllowedReturnUrl(viewClient, returnUrl))
        {
            return null;
        }

        return Results.Redirect(returnUrl);
    }

    private static bool IsAllowedReturnUrl(ViewClientOptions viewClient, string returnUrl)
    {
        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var candidate))
        {
            return false;
        }

        return Uri.TryCreate(viewClient.BaseUrl, UriKind.Absolute, out var allowed)
            && candidate.Scheme == allowed.Scheme
            && candidate.Host == allowed.Host
            && candidate.Port == allowed.Port;
    }

    private static IdentityResponse ToResponse(ClaimsPrincipal user) => new(
        user.FindFirst(ClaimTypes.Email)?.Value ?? user.Identity?.Name ?? string.Empty,
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
        user.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray());
}