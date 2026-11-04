# Publicar gratuitamente num computador Windows com Tailscale Funnel

Esta configuração mantém a aplicação e a base de dados no seu computador, sem abrir portas no router. O Tailscale fornece um URL HTTPS público em `*.ts.net`.

## Requisitos

- Windows 10/11 64-bit, ligado à corrente e à Internet;
- conta pessoal Tailscale gratuita;
- SQL Server Express e SQL Server Management Studio (SSMS);
- .NET SDK 10;
- este repositório local.

> O computador tem de permanecer ligado. Configure-o para nunca entrar em suspensão quando estiver ligado à corrente.

## 1. Preparar o Windows

Abra PowerShell como Administrador:

```powershell
winget install Microsoft.DotNet.SDK.10
powercfg /change standby-timeout-ac 0
powercfg /change hibernate-timeout-ac 0
```

Instale **SQL Server Express** e **SSMS** a partir da Microsoft. No instalador SQL Server selecione **Mixed Mode Authentication**, defina uma password forte para `sa` e adicione a sua conta Windows como administradora SQL. Não abra a porta 1433 no Windows Firewall: a base de dados deve continuar apenas local.

## 2. Criar a base de dados local

No PowerShell, a partir da raiz do repositório, execute o ficheiro SQL com uma password forte (sem plicas simples):

```powershell
sqlcmd -S .\SQLEXPRESS -E -v AppPassword="UMA_PASSWORD_FORTE" -i .\deploy\local-tailscale\create-local-database.sql
```

Se não tiver `sqlcmd`, abra o ficheiro `deploy/local-tailscale/create-local-database.sql` no SSMS, ative **Query > SQLCMD Mode**, substitua o valor da primeira linha e execute-o ligado a `localhost\SQLEXPRESS` com Windows Authentication.

## 3. Guardar segredos fora do repositório

Ainda em PowerShell de Administrador:

```powershell
.\deploy\local-tailscale\Set-CasamentoSecrets.ps1
```

Introduza a mesma password SQL usada no passo anterior, o email de administração e a password inicial do administrador da aplicação. O ficheiro criado em `C:\ProgramData\CasamentoTatianaDiogo\settings\appsettings.Local.json` só pode ser lido por Administradores e pelo sistema Windows; nunca o coloque no Git.

## 4. Instalar a aplicação como tarefa de arranque

No mesmo PowerShell de Administrador:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\deploy\local-tailscale\Install-CasamentoLocal.ps1
```

O script publica a versão Release em `C:\ProgramData\CasamentoTatianaDiogo\app`, cria a tarefa `CasamentoTatianaDiogo` para arrancar com o Windows e testa `http://127.0.0.1:5000/health`.

Para verificar a aplicação:

```powershell
Get-ScheduledTaskInfo -TaskName CasamentoTatianaDiogo
Invoke-WebRequest http://127.0.0.1:5000/health
```

## 5. Instalar e configurar Tailscale Funnel

1. Instale Tailscale para Windows e entre com a sua conta pessoal.
2. No painel de administração Tailscale, confirme que **MagicDNS**, **HTTPS certificates** e **Funnel** estão ativos.
3. Abra PowerShell como Administrador e execute:

```powershell
& "$env:ProgramFiles\Tailscale\tailscale.exe" funnel --bg http://127.0.0.1:5000
& "$env:ProgramFiles\Tailscale\tailscale.exe" funnel status
```

O segundo comando apresenta o URL público HTTPS. Guarde-o e partilhe-o com os convidados. A opção `--bg` torna a configuração persistente; volta a ser aplicada após reiniciar o computador ou Tailscale.

## Atualizar a aplicação

Depois de alterar o código:

```powershell
git pull --ff-only
Set-ExecutionPolicy -Scope Process Bypass
.\deploy\local-tailscale\Install-CasamentoLocal.ps1
```

## Operação e recuperação

```powershell
# Parar temporariamente
Stop-ScheduledTask -TaskName CasamentoTatianaDiogo

# Iniciar novamente
Start-ScheduledTask -TaskName CasamentoTatianaDiogo

# Ver o URL e estado público
& "$env:ProgramFiles\Tailscale\tailscale.exe" funnel status

# Desativar o acesso público
& "$env:ProgramFiles\Tailscale\tailscale.exe" funnel reset
```

Mantenha cópias de segurança regulares da base de dados e do ficheiro de configuração protegido. Como a aplicação recebe dados de convidados, instale as atualizações Windows, .NET, SQL Server e Tailscale quando estiverem disponíveis.
