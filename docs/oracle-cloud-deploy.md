# Publicar na Oracle Cloud Always Free

Este guia publica a aplicação ASP.NET Core numa VM Ubuntu 24.04. A aplicação é executada pelo `systemd`, escuta apenas em `127.0.0.1:5000` e o Caddy disponibiliza HTTPS público. Nenhuma palavra-passe é guardada no repositório.

## 1. Criar a VM

1. Crie uma conta Oracle Cloud e abra **Compute > Instances > Create instance**.
2. Escolha **Ubuntu 24.04** e uma forma marcada **Always Free eligible**. `VM.Standard.A1.Flex` (ARM) é uma boa escolha quando houver capacidade; `VM.Standard.E2.1.Micro` é a alternativa x86.
3. Adicione a sua chave pública SSH, mantenha uma IPv4 pública atribuída e crie a instância.
4. Na VCN da instância, abra a *Security List* da subnet e adicione regras TCP de entrada para as portas `80` e `443`, origem `0.0.0.0/0`. Mantenha a porta `22` limitada ao seu IP sempre que possível.
5. Guarde o IP público da VM. É este IP que deve ser autorizado na firewall do Azure SQL.

## 2. Preparar a base de dados Azure SQL

No recurso **SQL server** (não apenas na base de dados), abra **Networking** e adicione uma regra de firewall:

- início e fim: o IP público da VM Oracle;
- guarde a regra.

Para este alojamento, use autenticação SQL na aplicação. Crie ou use um utilizador SQL com permissões para criar/alterar as tabelas, pois a primeira execução inicializa a base de dados.

## 3. Ligar à VM e instalar dependências

No computador local:

```bash
ssh ubuntu@IP_PUBLICO_DA_VM
```

Na VM, execute:

```bash
sudo apt-get update
sudo apt-get upgrade -y
sudo apt-get install -y dotnet-sdk-10.0 git curl caddy ufw
sudo adduser --system --group --home /opt/casamento casamento
sudo ufw allow OpenSSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
dotnet --info
```

> Se tiver escolhido uma imagem diferente de Ubuntu 24.04, instale o SDK .NET 10 de acordo com a versão do Ubuntu antes de continuar.

## 4. Obter o código e configurar segredos

Na VM:

```bash
sudo mkdir -p /srv
sudo chown ubuntu:ubuntu /srv
git clone URL_DO_SEU_REPOSITORIO_GITHUB /srv/casamento-source
cd /srv/casamento-source
sudo install -d -o root -g casamento -m 0750 /etc/casamento
sudo install -o root -g casamento -m 0640 deploy/oracle/casamento.env.example /etc/casamento/casamento.env
sudo nano /etc/casamento/casamento.env
```

No ficheiro aberto, substitua todos os valores `SEU_...`, `SUA_...` e as credenciais do administrador. Não altere o nome `ConnectionStrings__DefaultConnection`.

Se ativar a galeria com Google Drive, copie o JSON da conta de serviço para `/etc/casamento/google-drive-service-account.json`, defina permissões `0640`, proprietário `root:casamento` e descomente a respetiva linha no ficheiro de ambiente.

## 5. Instalar o serviço da aplicação

Ainda dentro de `/srv/casamento-source`:

```bash
sudo install -m 0644 deploy/oracle/casamento.service /etc/systemd/system/casamento.service
sudo systemctl daemon-reload
sudo systemctl enable casamento
sudo chmod +x deploy/oracle/deploy.sh
sudo ./deploy/oracle/deploy.sh
sudo journalctl -u casamento -n 100 --no-pager
```

O último comando de publicação valida `http://127.0.0.1:5000/health`. Se falhar, não configure o domínio ainda: corrija primeiro a ligação Azure SQL com os registos apresentados por `journalctl`.

## 6. Configurar domínio e HTTPS

1. No fornecedor de DNS, crie um registo `A` para `wedding.example.com` (substitua pelo seu domínio) com o IP público da VM.
2. Espere pela propagação do DNS.
3. Na VM, edite o `deploy/oracle/Caddyfile` e substitua `wedding.example.com` pelo domínio real.
4. Instale e valide a configuração:

```bash
sudo install -m 0644 deploy/oracle/Caddyfile /etc/caddy/Caddyfile
sudo caddy validate --config /etc/caddy/Caddyfile
sudo systemctl enable --now caddy
sudo systemctl reload caddy
```

O Caddy obtém e renova automaticamente o certificado HTTPS quando o domínio resolve para a VM e as portas 80/443 estão abertas.

## Atualizações futuras

Na VM, para publicar uma nova versão:

```bash
cd /srv/casamento-source
git pull --ff-only
sudo ./deploy/oracle/deploy.sh
```

Para consultar o estado e os registos:

```bash
sudo systemctl status casamento
sudo journalctl -u casamento -f
sudo systemctl status caddy
```

## Notas de segurança e custo

- Não faça *commit* de `/etc/casamento/casamento.env`, ficheiros `*.publishsettings` ou credenciais Google.
- Uma VM Always Free não tem SLA. Mantenha uma cópia da configuração e dos dados essenciais.
- Um domínio próprio normalmente tem custo de registo; para HTTPS público estável, é necessário um domínio. Sem domínio, esta configuração não deve ser usada em produção.
