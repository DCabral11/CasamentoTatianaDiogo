# Isolated smoke tests: dotnet and prompts are replaced; no database is contacted.
$ErrorActionPreference = 'Stop'
$setup = Join-Path $PSScriptRoot '..\deploy\vercel\Initialize-Database.ps1'
$names = @('ASPNETCORE_ENVIRONMENT', 'Database__Provider', 'DATABASE_URL', 'DATABASE_URL_UNPOOLED',
    'POSTGRES_URL_NON_POOLING', 'PGHOST_UNPOOLED', 'POSTGRES_HOST_UNPOOLED', 'PGUSER', 'POSTGRES_USER',
    'PGDATABASE', 'POSTGRES_DATABASE', 'PGPASSWORD', 'POSTGRES_PASSWORD', 'AdminSeed__Email', 'AdminSeed__Password')
$original = @{}
foreach ($name in $names) { $original[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$direct = 'postgresql://owner:synthetic-password@ep-test.example.neon.tech/neondb?sslmode=require'
$previousState = $global:InitializeDatabaseTestState
$global:InitializeDatabaseTestState = @{ Direct = $direct; Pasted = ''; RunCount = 0; PromptCount = 0 }
function Read-Host {
    param([string]$Prompt, [switch]$AsSecureString)
    $global:InitializeDatabaseTestState.PromptCount++
    if ($AsSecureString) { return ConvertTo-SecureString $global:InitializeDatabaseTestState.Pasted -AsPlainText -Force }
    return ''
}
function dotnet {
    $global:InitializeDatabaseTestState.RunCount++
    if ($env:DATABASE_URL_UNPOOLED -ne $global:InitializeDatabaseTestState.Direct -and $env:POSTGRES_URL_NON_POOLING -ne $global:InitializeDatabaseTestState.Direct) { throw 'Normalization assertion failed.' }
    $global:LASTEXITCODE = 0
}
function RunCase([string]$value, [bool]$valid, [bool]$alias = $false) {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $null, 'Process') }
    $env:ASPNETCORE_ENVIRONMENT = 'OriginalEnvironment'
    $env:Database__Provider = 'OriginalProvider'
    if ($alias) { $env:POSTGRES_URL_NON_POOLING = $value }
    $global:InitializeDatabaseTestState.Pasted = $value
    $global:InitializeDatabaseTestState.PromptCount = 0
    $global:InitializeDatabaseTestState.RunCount = 0
    $failed = $false
    try { & $script:setup | Out-Null }
    catch {
        $failed = $true
        if ($_.Exception.Message.Contains('synthetic-password')) { throw 'Error leaked credentials.' }
        if ($valid) { throw ('A supported input was rejected: ' + $_.Exception.Message) }
    }
    if ($failed -eq $valid) { throw 'Unexpected validation result.' }
    if ($global:InitializeDatabaseTestState.RunCount -ne [int]$valid) { throw 'Setup ran before validation or did not run.' }
    if (-not $valid -and $global:InitializeDatabaseTestState.PromptCount -gt 1) { throw 'Administrator was prompted before URL validation.' }
    if ($env:ASPNETCORE_ENVIRONMENT -ne 'OriginalEnvironment' -or $env:Database__Provider -ne 'OriginalProvider' -or $env:DATABASE_URL_UNPOOLED) { throw 'Environment was not restored.' }
    if ($alias -and $env:POSTGRES_URL_NON_POOLING -ne $value) { throw 'Alias was not restored.' }
}
try {
    RunCase "  '$direct'  " $true
    RunCase "DATABASE_URL_UNPOOLED='$direct'" $true
    RunCase "POSTGRES_URL_NON_POOLING='$direct'" $true $true
    RunCase "psql '$direct'" $false
    RunCase '***********' $false
    RunCase 'ep-test.example.neon.tech' $false
    RunCase 'postgresql://owner:synthetic-password@ep-test-pooler.c-15.us-east-1.aws.neon.tech/db' $false
    Write-Output 'Passed: 7 initialization script cases; no database connections.'
}
finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $original[$name], 'Process') }
    $global:InitializeDatabaseTestState = $previousState
}
