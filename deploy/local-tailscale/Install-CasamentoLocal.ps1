[CmdletBinding()]
param(
    [string]$InstallPath = "$env:ProgramData\CasamentoTatianaDiogo\app"
)

$ErrorActionPreference = 'Stop'

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Abra o PowerShell como Administrador e execute novamente.'
    }
}

Assert-Administrator

$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$dotnetPath = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
$launcherPath = Join-Path $InstallPath 'start-site.cmd'
$taskName = 'CasamentoTatianaDiogo'

if (-not (Test-Path $dotnetPath)) {
    throw '.NET SDK/Runtime 10 não encontrado. Instale-o antes de continuar.'
}

if (-not (Test-Path "$env:ProgramData\CasamentoTatianaDiogo\settings\appsettings.Local.json")) {
    throw 'Falta a configuração secreta. Execute primeiro Set-CasamentoSecrets.ps1.'
}

if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) {
    Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}

New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
& $dotnetPath publish "$projectPath\CasamentoTatianaDiogo.csproj" --configuration Release --output $InstallPath
if ($LASTEXITCODE -ne 0) {
    throw 'A publicação .NET falhou.'
}

# Production schema setup is now an explicit installation step, including local hosting.
$setupVariables = @('ASPNETCORE_ENVIRONMENT', 'CASAMENTO_SETTINGS_PATH', 'Database__Provider')
$previousSetupValues = @{}
foreach ($name in $setupVariables) { $previousSetupValues[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $env:CASAMENTO_SETTINGS_PATH = "$env:ProgramData\CasamentoTatianaDiogo\settings\appsettings.Local.json"
    $env:Database__Provider = 'SqlServer'
    & $dotnetPath "$InstallPath\CasamentoTatianaDiogo.dll" --contentRoot $InstallPath --initialize-database
    if ($LASTEXITCODE -ne 0) { throw 'A preparação da base de dados falhou.' }
}
finally {
    foreach ($name in $setupVariables) { [Environment]::SetEnvironmentVariable($name, $previousSetupValues[$name], 'Process') }
}

Copy-Item "$PSScriptRoot\start-site.cmd" $launcherPath -Force

$taskAction = New-ScheduledTaskAction -Execute $env:ComSpec -Argument ('/c ""{0}""' -f $launcherPath)
$taskTrigger = New-ScheduledTaskTrigger -AtStartup
$taskSettings = New-ScheduledTaskSettingsSet -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1) -ExecutionTimeLimit (New-TimeSpan -Days 0)
$taskPrincipal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest

Register-ScheduledTask -TaskName $taskName -Action $taskAction -Trigger $taskTrigger -Settings $taskSettings -Principal $taskPrincipal -Description 'Site do casamento Tatiana e Diogo' -Force | Out-Null
Start-ScheduledTask -TaskName $taskName
Start-Sleep -Seconds 3

try {
    Invoke-WebRequest -UseBasicParsing http://127.0.0.1:5000/health | Out-Null
    Write-Host 'Aplicação publicada e disponível localmente em http://127.0.0.1:5000.' -ForegroundColor Green
}
catch {
    Write-Warning 'A tarefa foi criada, mas o health check falhou. Execute: Get-ScheduledTaskInfo -TaskName CasamentoTatianaDiogo'
    throw
}
