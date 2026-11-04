[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot '..\..\CasamentoTatianaDiogo.csproj'
$variableNames = @('ASPNETCORE_ENVIRONMENT', 'Database__Provider', 'DATABASE_URL', 'DATABASE_URL_UNPOOLED', 'AdminSeed__Email', 'AdminSeed__Password')
$previousValues = @{}
foreach ($name in $variableNames) { $previousValues[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }

try {
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $env:Database__Provider = 'PostgreSql'
    $directUrl = $env:DATABASE_URL_UNPOOLED -or $env:POSTGRES_URL_NON_POOLING
    $directParameters = ($env:PGHOST_UNPOOLED -or $env:POSTGRES_HOST_UNPOOLED) -and
        ($env:PGUSER -or $env:POSTGRES_USER) -and ($env:PGDATABASE -or $env:POSTGRES_DATABASE) -and
        ($env:PGPASSWORD -or $env:POSTGRES_PASSWORD)
    if (-not $directUrl -and -not $directParameters) {
        $secureUrl = Read-Host 'PostgreSQL direct/unpooled connection URL (without -pooler in the Neon hostname)' -AsSecureString
        $env:DATABASE_URL_UNPOOLED = [Net.NetworkCredential]::new('', $secureUrl).Password
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
