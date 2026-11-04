[CmdletBinding()]
param(
    [string]$SqlInstance = 'localhost\SQLEXPRESS',
    [string]$DatabaseName = 'CasamentoTatianaDiogo',
    [string]$SqlLogin = 'casamento_app'
)

$ErrorActionPreference = 'Stop'

function Get-PlainText([Security.SecureString]$SecureValue) {
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureValue)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
}

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Abra o PowerShell como Administrador e execute novamente.'
    }
}

Assert-Administrator

$sqlPassword = Get-PlainText (Read-Host 'Password do utilizador SQL casamento_app' -AsSecureString)
$adminEmail = Read-Host 'Email do administrador da aplicação'
$adminPassword = Get-PlainText (Read-Host 'Password inicial do administrador da aplicação' -AsSecureString)

if ([string]::IsNullOrWhiteSpace($sqlPassword) -or [string]::IsNullOrWhiteSpace($adminEmail) -or [string]::IsNullOrWhiteSpace($adminPassword)) {
    throw 'Todos os valores são obrigatórios.'
}

$settingsPath = "$env:ProgramData\CasamentoTatianaDiogo\settings"
$settingsFile = Join-Path $settingsPath 'appsettings.Local.json'
New-Item -ItemType Directory -Force -Path $settingsPath | Out-Null

$settings = [ordered]@{
    ConnectionStrings = [ordered]@{
        DefaultConnection = "Server=$SqlInstance;Database=$DatabaseName;User ID=$SqlLogin;Password=$sqlPassword;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
    }
    AdminSeed = [ordered]@{
        Email = $adminEmail
        Password = $adminPassword
    }
}

$settings | ConvertTo-Json -Depth 4 | Set-Content -Path $settingsFile -Encoding UTF8

$acl = New-Object System.Security.AccessControl.FileSecurity
$acl.SetAccessRuleProtection($true, $false)
$acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule('BUILTIN\Administrators', 'FullControl', 'Allow')))
$acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule('NT AUTHORITY\SYSTEM', 'FullControl', 'Allow')))
Set-Acl -Path $settingsFile -AclObject $acl

Write-Host "Configuração protegida criada em $settingsFile." -ForegroundColor Green
