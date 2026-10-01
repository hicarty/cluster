using System.Text.Encodings.Web;

namespace Cluster.Identity.Endpoints;

/// <summary>
/// Minimal server-rendered HTML for the identity pages. No Blazor, no Razor, no client-side
/// scripts: the whole login journey is plain form posts so it stays debuggable from a terminal.
/// </summary>
internal static class Html
{
    public static string Page(string title, string heading, string body) => $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1.0" />
        <title>{{HtmlEncoder.Default.Encode(title)}} | Cluster.Identity</title>
        <style>
        :root { color-scheme: light dark; }
        body { font-family: system-ui, -apple-system, "Segoe UI", sans-serif; margin: 0; display: grid;
               place-items: center; min-height: 100vh; background: #f6f6f7; color: #1b1b1f; }
        main { background: #fff; padding: 2rem; border-radius: .75rem; box-shadow: 0 1px 3px rgba(0,0,0,.2);
               width: min(24rem, 92vw); }
        h1 { margin: 0 0 1.25rem; font-size: 1.5rem; }
        label { display: block; margin: .85rem 0 .25rem; font-size: .875rem; font-weight: 600; }
        input[type=email], input[type=password] { width: 100%; padding: .55rem .65rem; font-size: 1rem;
                border: 1px solid #b8b8bd; border-radius: .375rem; box-sizing: border-box; }
        label.check { display: flex; align-items: center; gap: .4rem; font-weight: 400; }
        button { margin-top: 1.25rem; width: 100%; padding: .6rem 1rem; font-size: 1rem; font-weight: 600;
                 color: #fff; background: #512bd4; border: 0; border-radius: .375rem; cursor: pointer; }
        button:hover { background: #4024a8; }
        .error { background: #fde7e9; color: #8e1b2b; padding: .6rem .75rem; border-radius: .375rem; }
        .ok { background: #e7f6ec; color: #1b5e33; padding: .6rem .75rem; border-radius: .375rem; }
        .alt { margin-top: 1.25rem; font-size: .875rem; color: #55555c; }
        a { color: #512bd4; }
        footer { margin-top: 1.5rem; font-size: .75rem; color: #8a8a90; }
        </style>
        </head>
        <body>
        <main>
        <h1>{{HtmlEncoder.Default.Encode(heading)}}</h1>
        {{body}}
        <footer>Cluster.Identity &middot; ASP.NET Core Identity</footer>
        </main>
        </body>
        </html>
        """;
}