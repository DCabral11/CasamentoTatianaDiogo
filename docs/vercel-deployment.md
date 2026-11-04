# Deploy to Vercel

The application runs as a .NET 10 Linux container on Vercel with an external PostgreSQL database. Vercel's Container Runtime is currently in beta. The root `Dockerfile` and `vercel.json` select the Container preset, HTTP port 8080 and the Paris (`cdg1`) region. Place the database nearby.

## 1. Create the database

Create PostgreSQL through the Vercel Marketplace (for example, Neon) or use an existing PostgreSQL service. Connect it to the Vercel project and obtain the connection URL. The application accepts `postgres://` and `postgresql://` URLs, including percent-encoded usernames/passwords, `sslmode` and `channel_binding` parameters. It also accepts an Npgsql connection string through `ConnectionStrings__DefaultConnection` when `DATABASE_URL` is absent.

Use `DATABASE_URL` for the runtime connection. A provider's pooled URL is suitable for requests; the application uses a small local connection pool. Set `DATABASE_URL_UNPOOLED` for the database setup command if the provider supplies a direct connection. The initialization command prefers that direct URL. Use TLS for remote databases; URLs default to certificate-verified TLS when `sslmode` is omitted.

This prepares a **new PostgreSQL database**. Existing SQL Server records are not automatically copied or deleted. Back up the existing database and transfer any existing guests, RSVP records, settings and accounts before switching a live site. SQL Server remains supported through `Database__Provider=SqlServer` and `ConnectionStrings__DefaultConnection`; override the PostgreSQL setting in Vercel if keeping an externally reachable SQL Server instead. A local SQL Server/LocalDB instance is not reachable from Vercel.

## 2. Initialize the database once

From the repository root on a computer with .NET SDK 10:

```powershell
.\deploy\vercel\Initialize-Database.ps1
```

The script prompts for the database URL, administrator email and password, runs PostgreSQL migrations and seeds the site settings. It restores the previous process environment afterward and does not write passwords to files. The password needs at least 12 characters, uppercase and lowercase letters, a digit and a symbol. Production setup does not create the development test guests. Keep the selected administrator credentials for login.

Alternatively, set `ASPNETCORE_ENVIRONMENT=Production`, `Database__Provider=PostgreSql`, `DATABASE_URL` (or `DATABASE_URL_UNPOOLED`), `AdminSeed__Email` and `AdminSeed__Password` in the process environment, then run:

```powershell
dotnet run --configuration Release --no-launch-profile -- --initialize-database
```

Repeat this command before deploying changes that introduce a database migration. Initialization is an explicit operation: normal production startup does not migrate or seed the database. Do not run it during every container start or attach production initialization to preview builds. Initialization is also required once for a new production SQL Server database.

## 3. Configure Vercel and deploy

Import the Git repository into Vercel. Use the repository root as the project root and **Container** as the framework preset. Leave the build and output directory overrides unset; Vercel builds the Dockerfile. The project configuration supplies the non-secret port and runtime settings.

Add these environment variables in Project Settings:

| Variable | Purpose |
| --- | --- |
| `DATABASE_URL` | Required PostgreSQL runtime connection URL. Marketplace integrations may supply it automatically. |
| `GoogleDrive__CredentialsBase64` | Optional service-account JSON encoded as Base64; required when enabling photos unless another supported credential format is configured. |
| `RsvpEmail__*` | Optional SMTP configuration; see `.env.example`. Notifications are disabled by default. |

The bootstrap admin variables are only needed in the environment running the setup command, and can be removed afterward. `.env.example` lists the available secret settings. ASP.NET does not automatically load `.env` files; use process environment variables for local commands. Keep preview deployments on their own database or database branch so they cannot change production RSVPs. Do not point an uninitialized preview database at the app and expect startup to create its tables.

Deploy through the Vercel Git integration or `vercel --prod`. The app stores authentication/antiforgery keys in `DataProtectionKeys`, so different instances can read the same login cookies and form tokens. Do not set a filesystem key path on Vercel. `Hosting__Vercel=true` is supplied by `vercel.json` to process HTTPS headers from Vercel's managed ingress; only enable this setting when the container is behind that trusted ingress.

After deployment, verify:

- `/health/live` returns 200 when the application process is running.
- `/health` returns 200 when the database can be queried.
- The home, information and RSVP pages load, and administrator login works.
- A test RSVP saves successfully.
- If photos are enabled, send a photo larger than 4.5 MB and check it appears in the admin list and Drive.

## 4. Enable Google Drive photos

Enable the Google Drive API for a Google Cloud project and create a service account. Use a Google Workspace **Shared drive**, grant that service account sufficient access, and enter the destination folder ID under Admin > Photos > Settings. Service accounts do not have personal Drive storage quota; a personal My Drive folder shared with the account is insufficient for this service-account-only setup.

Set either `GoogleDrive__CredentialsBase64` or `GoogleDrive__CredentialsJson` in Vercel. For example, to copy the Base64 value locally without displaying it:

```powershell
$credentialBytes = [IO.File]::ReadAllBytes('C:\private\drive-service-account.json')
[Convert]::ToBase64String($credentialBytes) | Set-Clipboard
```

Paste it as the environment variable value, clear the clipboard, and redeploy. Keep the original credentials file outside the repository. `GoogleDrive__CredentialsPath` remains available for local hosting; the Vercel image excludes credential files. Never put the private key into browser code.

The browser sends 2 MiB chunks to the application, which forwards them through Drive's resumable upload API. Every request remains below Vercel's 4.5 MB body limit, while the configured per-photo limit (15 MB by default) is preserved. Upload positions and expiring session tokens work across instances. Interrupted chunks can be retried without duplicate photo records; the page reports progress and allows retrying. JavaScript is required for large uploads; the fallback form accepts less than 4 MB combined. Upload windows use `Europe/Lisbon`, including summer time.

## Local verification

```powershell
dotnet test tests/CasamentoTatianaDiogo.Tests/CasamentoTatianaDiogo.Tests.csproj
dotnet publish CasamentoTatianaDiogo.csproj -c Release
docker build -t casamento-vercel .
docker run --rm --env-file .env.docker -p 8080:8080 casamento-vercel
```

For the last command, create an ignored `.env.docker` containing real `DATABASE_URL` and any optional integration settings. Initialize the target database first. Use one `NAME=value` entry per line without shell assignments or surrounding quotes. The container runs as a non-root user. Application files are read-only at runtime; only request buffering may use temporary scratch space.

The regression suite checks PostgreSQL migrations and timestamp mappings, shared protection keys, Lisbon daylight saving time, upload limits, chunk retries and HTTP routes. Its isolated application database is SQLite; it does not contact your production database or Drive account.

## References

- [Vercel Container Runtime](https://vercel.com/docs/functions/runtimes/container)
- [Vercel Function limits](https://vercel.com/docs/functions/limitations)
- [Postgres on Vercel](https://vercel.com/docs/postgres)
- [Google Drive resumable uploads](https://developers.google.com/workspace/drive/api/guides/manage-uploads)
- [Google Shared drive support](https://developers.google.com/workspace/drive/api/guides/enable-shareddrives)
