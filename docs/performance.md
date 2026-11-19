# Reducing Vercel compute usage

## Static assets

On Vercel, the `assets` service publishes `wwwroot` directly to the CDN. Explicit rewrites route images, CSS, JavaScript, libraries and the favicon to static storage, including on a cache miss. Other requests go to the `site` container. Select the **Services** framework preset in the Vercel project before deploying this configuration; see [deployment setup](vercel-deployment.md#3-configure-vercel-and-deploy).

The public URLs and Razor's content hashes remain unchanged. `vercel.json` supplies browser cache headers for the static service. `StaticAssetCache` retains the equivalent policy when ASP.NET serves files locally or on another host. These policies do not add shared-cache headers to HTML, RSVP endpoints, administrator pages or uploads.

| Asset URL | Browser cache |
| --- | --- |
| Versioned, such as `/js/site.js?v=...` | 365 days, immutable |
| Unversioned, such as `/images/navbar.png` | 1 hour |

Vercel stores static build assets independently of the container and handles their CDN lifetime. The ASP.NET fallback uses `Vercel-CDN-Cache-Control` (365 days for versioned URLs, one day otherwise) when deployed without a separate static service.

Razor's existing `asp-append-version` hashes CSS/JS contents, so changing a script or stylesheet produces a new URL. Static-file ETags and conditional requests remain available. Unversioned files should get a new filename or versioned URL when an immediate browser refresh is required.

After deployment, request the same asset URL twice and inspect `x-vercel-cache` for a `HIT`. A production CDN hit cannot be verified through the local integration test server. Compare Vercel's daily Active CPU and invocation counts with comparable traffic before and after deploying.

## Container startup

The Dockerfile publishes Linux x64 ReadyToRun assemblies, with `--self-contained false` so the image continues using the shared ASP.NET runtime. The restore step requests the same runtime and compilation mode as publish. Disable this compilation mode with `--build-arg PUBLISH_READY_TO_RUN=false` when comparing images or rolling back; larger image size can offset startup gains, so verify both latency and CPU in production.

A local Docker Desktop comparison on 2026-11-19 used three alternating starts of each image and an isolated, disposable PostgreSQL containing only the DataProtection key table. The endpoint was `/health/live`; CPU was read from the application's cgroup immediately after readiness.

| Median / size | Without ReadyToRun | With ReadyToRun |
| --- | --- | --- |
| Start to readiness, including Docker launch and readiness probing | 1.751 s | 1.438 s |
| Container CPU used by readiness | 1.362 s | 0.980 s |
| Image size reported by Docker | 139.7 MB | 157.6 MB |

This is approximately 18% less startup time and 28% less startup CPU in this local sample. It excludes production networking, image downloads, Neon wake-up latency, full page generation and subsequent traffic. It does not establish an equivalent reduction in the monthly Vercel quota. Both images successfully served the liveness endpoint and the local static-file fallback; the temporary containers, database and network were removed after the comparison.

Npgsql connections skip Kerberos/GSSAPI probing by default, removing repeated `libgssapi_krb5.so.2` messages. TLS mode and channel binding are preserved. Explicit GSS settings remain supported for servers that need them.

`CarouselPhotoService` caches the ordered photo list and responsive-image metadata for five minutes per instance, instead of enumerating files on each page request. File-provider change tokens refresh the list when source photos or generated variants change, including when adding photos to an empty album.

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
npm ci --prefix tests/vercel --ignore-scripts --no-audit --no-fund
npm test --prefix tests/vercel
dotnet test tests/CasamentoTatianaDiogo.Tests/CasamentoTatianaDiogo.Tests.csproj --configuration Release
dotnet publish CasamentoTatianaDiogo.csproj --configuration Release
```

Regression coverage includes warm-page query counts, edit invalidation, expiration and concurrent cache fills, private FAQ previews, static cache/ETag headers, music routing, uncached deadlines and photo permissions, RSVP conflicts, and browser presence timing. The browser-presence JavaScript tests use Node's built-in runner without additional packages and run in the deployment verification workflow.

The separate `tests/vercel` project pins Vercel's own service resolver and routing compiler. It checks the resolved static/container builders, every actual public asset path, versioned cache rules, and the routing/privacy of dynamic endpoints. Carousel tests verify reuse and invalidation after file changes; connection tests verify that disabling GSS probing preserves TLS and explicit GSS options.
