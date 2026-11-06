[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot '..\..\CasamentoTatianaDiogo.csproj'
$variableNames = @('ASPNETCORE_ENVIRONMENT', 'Database__Provider', 'DATABASE_URL', 'DATABASE_URL_UNPOOLED', 'POSTGRES_URL_NON_POOLING', 'AdminSeed__Email', 'AdminSeed__Password')
$previousValues = @{}
foreach ($name in $variableNames) { $previousValues[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }

try {
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $env:Database__Provider = 'PostgreSql'
    $directUrl = -not [string]::IsNullOrWhiteSpace($env:DATABASE_URL_UNPOOLED) -or
        -not [string]::IsNullOrWhiteSpace($env:POSTGRES_URL_NON_POOLING)
    $directParameters = ($env:PGHOST_UNPOOLED -or $env:POSTGRES_HOST_UNPOOLED) -and
        ($env:PGUSER -or $env:POSTGRES_USER) -and ($env:PGDATABASE -or $env:POSTGRES_DATABASE) -and
        ($env:PGPASSWORD -or $env:POSTGRES_PASSWORD)
    if (-not $directUrl -and -not $directParameters) {
        Write-Host 'Cola apenas o URL completo postgresql://... da ligação direta do Neon, sem o comando psql e sem -pooler no hostname.'
        $secureUrl = Read-Host 'PostgreSQL direct/unpooled connection URL' -AsSecureString
        $env:DATABASE_URL_UNPOOLED = [Net.NetworkCredential]::new('', $secureUrl).Password
    }
    # Validate pasted URLs before requesting administrator credentials or starting migrations.
    # Errors deliberately exclude the input and the parser exception.
    foreach ($name in @('DATABASE_URL_UNPOOLED', 'POSTGRES_URL_NON_POOLING')) {
        $value = [Environment]::GetEnvironmentVariable($name, 'Process')
        if ([string]::IsNullOrWhiteSpace($value)) { continue }
        $value = $value.Trim()
        if ($value.Length -ge 2 -and (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'")))) { $value = $value.Substring(1, $value.Length - 2).Trim() }
        $value = $value -replace '^(?:export\s+|\$env:)?(?:DATABASE_URL(?:_UNPOOLED)?|POSTGRES_URL(?:_NON_POOLING)?|POSTGRES_PRISMA_URL)\s*=\s*', ''
        if ($value.Length -ge 2 -and (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'")))) { $value = $value.Substring(1, $value.Length - 2).Trim() }
        if ($value -match '[\r\n]' -or $value -notmatch '^(?:postgres(?:ql)?://|[A-Za-z][A-Za-z ]*\s*=)') {
            throw 'Formato de ligação inválido. Cola o URL completo postgresql://utilizador:password@host/base, numa só linha; não coles o comando psql, asteriscos ou apenas o hostname.'
        }
        if ($value -match '^postgres(?:ql)?://') {
            $parsedUrl = $null
            if (-not [Uri]::TryCreate($value, [UriKind]::Absolute, [ref]$parsedUrl) -or
                [string]::IsNullOrWhiteSpace($parsedUrl.Host) -or $parsedUrl.UserInfo -notmatch '^.+:' -or $parsedUrl.AbsolutePath.Length -le 1 -or $parsedUrl.Fragment) {
                throw 'URL PostgreSQL inválido: confirma o utilizador, password, hostname e nome da base de dados. Usa o URL copiado do painel Neon.'
            }
            if ($parsedUrl.Host -match '-pooler\..*\.neon\.tech$') { throw 'A inicialização requer a ligação direta do Neon, sem -pooler no hostname.' }
        }
        [Environment]::SetEnvironmentVariable($name, $value, 'Process')
        break # Match application precedence; an unused alias must not block a valid direct URL.
    }
    if (-not $env:AdminSeed__Email) {
        $env:AdminSeed__Email = Read-Host 'Initial administrator email (leave blank if an administrator already exists)'
    }
    if ($env:AdminSeed__Email -and -not $env:AdminSeed__Password) {
        $securePassword = Read-Host 'Initial administrator password (at least 12 characters, upper/lowercase, number and symbol)' -AsSecureString
        $env:AdminSeed__Password = [Net.NetworkCredential]::new('', $securePassword).Password
    }
    & dotnet run --project $projectPath --configuration Release --no-launch-profile -- --initialize-database
    if ($LASTEXITCODE -ne 0) { throw 'Database setup failed. Check the error above before deploying.' }
}
finally {
    foreach ($name in $variableNames) { [Environment]::SetEnvironmentVariable($name, $previousValues[$name], 'Process') }
}
