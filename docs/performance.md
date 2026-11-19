# Reducing Vercel compute usage

## Static assets

`StaticAssetCache` applies headers only to files served by ASP.NET's static-file middleware, before authentication and MVC. It does not apply shared-cache headers to HTML, RSVP endpoints, administrator pages or uploads.

| Asset URL | Browser cache | Vercel CDN cache |
| --- | --- | --- |
| Versioned, such as `/js/site.js?v=...` | 365 days, immutable | 365 days, immutable |
| Unversioned, such as `/images/navbar.png` | 1 hour | 1 day |

Razor's existing `asp-append-version` hashes CSS/JS contents, so changing a script or stylesheet produces a new URL. Static-file ETags and conditional requests remain available. Unversioned files should get a new filename or versioned URL when an immediate browser refresh is required.

After deployment, request the same asset URL twice and inspect `x-vercel-cache` for a `HIT`. A production CDN hit cannot be verified through the local integration test server. Compare Vercel's daily Active CPU and invocation counts with comparable traffic before and after deploying.

## Public presentation data

`PublicContentCache` stores wedding presentation settings, site text, FAQ settings, published FAQ items and the public timeline for 30 seconds per application instance. Concurrent cache misses share a load; failed loads are retried rather than cached. No scoped database context or loading task is retained in the cache.

Successful tracked saves of these entities invalidate that instance's cache through `PublicContentCacheInvalidationInterceptor`, for PostgreSQL and SQL Server. Generation keys prevent an older in-flight read from refilling the current cache after an edit. Other Vercel instances refresh when their short cache entries expire. Bulk SQL updates bypass the interceptor and rely on expiration for presentation data.

RSVP deadlines, verification/access checks, guest responses, photo-upload permissions and administrative edit/preview data continue to use current database reads. The time-dependent wedding phase is calculated for every page rather than caching rendered HTML. FAQ drafts use a separate uncached preview path.

## Music and RSVP presence

Public entry pages render the music shell before loading the inner page model. Deep RSVP links retain the result filter so their redirects, cookies and access validation still execute normally. Error responses and calendar downloads are not wrapped.

The RSVP editor checks for another editor every 60 seconds while active. It stops after three minutes without form interaction, when hidden, when leaving/submitting the page, or after detecting a conflict. It resumes on interaction or page restoration and avoids duplicate/in-flight requests. Presence remains advisory; the database version check at save time is still mandatory.

## Verification

```powershell
node --test tests/rsvp-presence.Tests.cjs
dotnet test tests/CasamentoTatianaDiogo.Tests/CasamentoTatianaDiogo.Tests.csproj --configuration Release
dotnet publish CasamentoTatianaDiogo.csproj --configuration Release
```

Regression coverage includes warm-page query counts, edit invalidation, expiration and concurrent cache fills, private FAQ previews, static cache/ETag headers, music routing, uncached deadlines and photo permissions, RSVP conflicts, and browser presence timing. The JavaScript tests use Node's built-in runner without additional packages and run in the deployment verification workflow.
