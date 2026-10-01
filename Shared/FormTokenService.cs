using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Cluster.Identity.Shared;

/// <summary>
/// Double-submit CSRF protection for the identity forms.
///
/// The framework antiforgery service embeds a fingerprint of the signed-in user in its token,
/// which makes a token issued while signed out invalid the moment the user signs in. Since these
/// forms exist precisely to change the signed-in state, a plain double-submit token is used instead:
/// a random nonce is written to an HttpOnly cookie and echoed in a hidden form field, and both must
/// match on submit.
/// </summary>
internal sealed class FormTokenService
{
    public const string CookieName = "Cluster.Identity.FormToken";

    private const string FieldName = "__FormToken";

    private readonly IHttpContextAccessor httpContextAccessor;

    public FormTokenService(IHttpContextAccessor httpContextAccessor)
        => this.httpContextAccessor = httpContextAccessor;

    public string Issue()
    {
        var token = RandomNumberGenerator.GetBytes(32);

        var nonce = WebEncoders.Base64UrlEncode(token);
        var context = httpContextAccessor.HttpContext!;

        context.Response.Cookies.Append(
            CookieName,
            nonce,
            new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/identity",
            });

        return FieldName + "|" + nonce;
    }

    public bool Validate(string? submittedToken)
    {
        if (string.IsNullOrEmpty(submittedToken))
        {
            return false;
        }

        var parts = submittedToken.Split('|', 2);

        if (parts.Length != 2 || parts[0] != FieldName)
        {
            return false;
        }

        var context = httpContextAccessor.HttpContext!;
        var cookieToken = context.Request.Cookies[CookieName];

        if (string.IsNullOrEmpty(cookieToken) || cookieToken.Length != parts[1].Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(cookieToken),
            Encoding.UTF8.GetBytes(parts[1]));
    }

    public string FieldTokenName => FieldName;
}