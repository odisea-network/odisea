using Microsoft.EntityFrameworkCore;
using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Entities;

namespace Odisea.WebAPI.Middleware;

/// <summary>
/// Enforces per-Publication origin allowlists on the public embed endpoints
/// (manifest + resolved offers). When a Publication lists no allowed domains the
/// request passes through with a logged warning, so onboarding an embed is a
/// one-step affair; once domains are listed, the request Origin (Referer
/// fallback) host must match one of them or the request is rejected 403.
/// </summary>
public class EmbedSecurityMiddleware(RequestDelegate next, ILogger<EmbedSecurityMiddleware> logger)
{
    private const string ManifestPrefix = "/api/v1/publications/";
    private const string CollectionsPrefix = "/api/v1/collections/";

    public async Task InvokeAsync(HttpContext context, IAppDbContext db)
    {
        var match = await ResolveAllowedDomainsAsync(context, db);

        // Not a guarded embed endpoint (or target not found) — let routing handle it.
        if (match is not { } domains)
        {
            await next(context);
            return;
        }

        if (domains.Count == 0)
        {
            logger.LogWarning(
                "Embed request to {Path} served with no allowed-domain restriction (open publication).",
                context.Request.Path);
            await next(context);
            return;
        }

        var host = RequestHost(context.Request);

        // No Origin/Referer = not a browser cross-origin request (e.g. server-side
        // fetch); the allowlist only governs browser embedding, so allow it.
        if (string.IsNullOrEmpty(host))
        {
            await next(context);
            return;
        }

        if (domains.Any(d => d.Matches(host)))
        {
            await next(context);
            return;
        }

        logger.LogWarning("Embed request from disallowed origin {Host} to {Path}.",
            host, context.Request.Path);
        await WriteForbiddenAsync(context, host);
    }

    /// <summary>
    /// Returns the allowed-domain list governing this request, or null if the path
    /// is not a guarded embed endpoint or the target publication does not exist.
    /// </summary>
    private static async Task<List<AllowedDomain>?> ResolveAllowedDomainsAsync(
        HttpContext context, IAppDbContext db)
    {
        if (!HttpMethods.IsGet(context.Request.Method))
            return null;

        var path = context.Request.Path;

        // GET /api/v1/publications/{key} — single publication keyed by the route.
        if (path.StartsWithSegments(ManifestPrefix.TrimEnd('/'), out var rest) &&
            TrySingleSegment(rest, out var key) && !Guid.TryParse(key, out _))
        {
            var pub = await db.Publications.AsNoTracking()
                .Include(p => p.AllowedDomains)
                .FirstOrDefaultAsync(p => p.Key == key, context.RequestAborted);
            return pub?.AllowedDomains;
        }

        // GET /api/v1/collections/{slug}/offers — union the domains of every
        // publication backing that collection. Any one that lists the origin lets
        // the request through; if none list any, the collection is open.
        if (path.StartsWithSegments(CollectionsPrefix.TrimEnd('/'), out var collRest) &&
            TryCollectionOffers(collRest, out var slug))
        {
            var collectionId = await db.Collections.AsNoTracking()
                .Where(c => c.Slug == slug)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(context.RequestAborted);

            if (collectionId is null)
                return null;

            return await db.AllowedDomains.AsNoTracking()
                .Where(d => db.Publications
                    .Any(p => p.Id == d.PublicationId && p.CollectionId == collectionId))
                .ToListAsync(context.RequestAborted);
        }

        return null;
    }

    private static bool TrySingleSegment(PathString rest, out string segment)
    {
        segment = rest.Value?.Trim('/') ?? string.Empty;
        return segment.Length > 0 && !segment.Contains('/');
    }

    private static bool TryCollectionOffers(PathString rest, out string slug)
    {
        slug = string.Empty;
        var parts = rest.Value?.Trim('/').Split('/') ?? [];
        if (parts.Length != 2 || parts[1] != "offers") return false;
        slug = parts[0];
        return slug.Length > 0;
    }

    private static string RequestHost(HttpRequest request)
    {
        var origin = request.Headers.Origin.ToString();
        if (string.IsNullOrEmpty(origin))
            origin = request.Headers.Referer.ToString();

        return Uri.TryCreate(origin, UriKind.Absolute, out var uri) ? uri.Host : string.Empty;
    }

    private static async Task WriteForbiddenAsync(HttpContext context, string host)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://httpstatuses.io/403",
            title = "Origin not permitted",
            status = 403,
            detail = $"Origin '{host}' is not allowed to embed this publication.",
        });
    }
}
