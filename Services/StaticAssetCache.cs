using Microsoft.AspNetCore.StaticFiles;

namespace CasamentoTatianaDiogo.Services;

public static class StaticAssetCache
{
    public static void PrepareResponse(StaticFileResponseContext context)
    {
        var versioned = !string.IsNullOrWhiteSpace(context.Context.Request.Query["v"]);
        var headers = context.Context.Response.Headers;
        // Versioned CSS/JS URLs change with the file contents. Unversioned image
        // URLs have a shorter browser lifetime so replacing an image stays practical.
        headers.CacheControl = versioned
            ? "public,max-age=31536000,immutable"
            : "public,max-age=3600";
        headers["Vercel-CDN-Cache-Control"] = versioned
            ? "public,s-maxage=31536000,immutable"
            : "public,s-maxage=86400";
    }
}
