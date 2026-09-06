# Deploy no Azure App Service

## Pré-requisitos

Cria um Azure App Service com runtime .NET 10 e uma Azure SQL Database acessível pela aplicação. Configura o Health check do App Service com o caminho `/health`.

## Variáveis de ambiente

No App Service, em **Environment variables**, define os seguintes valores. Não guardes segredos em `appsettings.json` nem no repositório.

| Nome | Obrigatório | Descrição |
|---|---:|---|
| `SQLAZURECONNSTR_DefaultConnection` | Sim | Connection string da Azure SQL Database. |
| `AdminSeed__Email` | Sim, no primeiro deploy | E-mail da primeira conta de administração. |
| `AdminSeed__Password` | Sim, no primeiro deploy | Palavra-passe forte da primeira conta de administração. |
| `RsvpEmail__Enabled` | Não | `true` para ativar as notificações de RSVP. |
| `RsvpEmail__Host`, `RsvpEmail__Username`, `RsvpEmail__Password`, `RsvpEmail__From` | Se e-mail ativo | Credenciais SMTP. Usa referências de Azure Key Vault para valores sensíveis. |
| `GoogleDrive__CredentialsPath` | Se a galeria estiver ativa | Caminho para credenciais do Google Drive disponibilizadas de forma segura. |

Depois do primeiro arranque, remove `AdminSeed__Password` do App Service. A conta já criada não é alterada.

## GitHub Actions

O workflow `.github/workflows/azure-app-service.yml` publica automaticamente os commits em `main`. Cria estes secrets no repositório:

| Secret | Valor |
|---|---|
| `AZURE_WEBAPP_NAME` | Nome do App Service. |
| `AZURE_WEBAPP_PUBLISH_PROFILE` | Conteúdo completo do publish profile descarregado no portal Azure. |

O workflow compila e publica a aplicação em modo Release. A configuração de produção e as connection strings são lidas no próprio Azure App Service.
