# Verificação do RSVP por email

O QR e o endereço geral dos convites mantêm-se. O convidado pesquisa o nome, indica o email e introduz o código recebido antes de consultar ou guardar a resposta. A aplicação envia códigos apenas por email.

## Onde obter os valores: exemplo com Gmail

Usa uma conta Gmail à qual tenhas acesso, por exemplo a conta do casamento. O email remetente pertence aos noivos/à aplicação; o email de cada convidado é introduzido no formulário e não é uma variável da Vercel.

1. Inicia sessão na conta que enviará os códigos.
2. Abre [Segurança da Conta Google](https://myaccount.google.com/security) e ativa a **Verificação em dois passos**.
3. Abre [Palavras-passe de aplicação](https://myaccount.google.com/apppasswords). Cria uma entrada com um nome identificável, por exemplo `Casamento Vercel`.
4. Copia a palavra-passe gerada de 16 caracteres, sem os espaços de apresentação, diretamente para `RsvpEmail__Password` na Vercel. Não uses a palavra-passe normal do Gmail.

Se essa opção não aparecer, consulta a [ajuda da Google](https://support.google.com/accounts/answer/185833?hl=pt): algumas contas de organizações, Proteção Avançada ou configurações de autenticação restringem o acesso. Nesses casos, o administrador da conta ou outro fornecedor SMTP terá de disponibilizar credenciais compatíveis.

## Variáveis a configurar na Vercel

Introduz os nomes com **dois underscores** (`__`), exatamente como estão nesta tabela. Os valores são texto simples, sem aspas à volta.

| Nome | Valor para Gmail | Onde obter / finalidade |
| --- | --- | --- |
| `RsvpVerification__Email__Enabled` | `true` | Ativa os códigos de verificação na aplicação. |
| `RsvpEmail__Host` | `smtp.gmail.com` | Servidor SMTP público da Google. |
| `RsvpEmail__Port` | `587` | Porta SMTP com STARTTLS. |
| `RsvpEmail__UseSsl` | `true` | Ativa TLS na ligação SMTP. |
| `RsvpEmail__Username` | O email Gmail completo do remetente | A conta Google em que criaste a palavra-passe de aplicação. |
| `RsvpEmail__Password` | Palavra-passe de aplicação de 16 caracteres | Gerada na página de palavras-passe de aplicação da Google; guardar como **Secret**. |
| `RsvpEmail__From` | O mesmo email de `Username` | Endereço que aparecerá como remetente dos códigos. |
| `RsvpEmail__Enabled` | `false` ou `true` | Opcional: `true` também envia notificações das respostas aos noivos. É independente dos códigos de verificação. |
| `RsvpEmail__Recipient` | Email dos noivos | Opcional: configurar quando as notificações aos noivos estão ativas. Não é o destinatário dos códigos dos convidados. |

Para outro fornecedor, procura no painel desse fornecedor as **credenciais SMTP**: servidor, porta com STARTTLS, utilizador, password/chave SMTP e remetente autorizado. Mantém `RsvpVerification__Email__Enabled=true` e usa esses dados nos campos `RsvpEmail__*`. Este exemplo usa a porta 587; a Vercel permite SMTP de saída nessa porta e bloqueia a porta 25.

## Como configurar no painel Vercel

1. Abre o projeto na [Vercel](https://vercel.com/dashboard).
2. Entra em **Settings → Environment Variables** e escolhe **Add Environment Variable** / **Add New**.
3. Para cada linha obrigatória da tabela, preenche **Key/Name** e **Value**. Seleciona **Production** para o site público. Se precisares de testar em Preview, configura também esse ambiente com as credenciais adequadas.
4. Para `RsvpEmail__Password`, escolhe o tipo **Secret** quando essa opção estiver disponível. Guarda as variáveis.
5. Publica esta versão do código. Se já estiver publicada, vai a **Deployments**, abre o menu do deployment pretendido e escolhe **Redeploy**. As alterações de variáveis só entram em vigor num novo deployment.
6. Se configuraste variáveis antigas de envio por telemóvel, podes removê-las; esta versão não as usa.

Os valores reais não devem ser colocados em `appsettings.json`, no Git ou na conversa. `.env.example` documenta os nomes, mas a aplicação não carrega esse ficheiro automaticamente.

## Base de dados e validação

A alteração para email não exige uma migração adicional. A migração `AddRsvpContactVerification`, criada para a verificação dos convites, continua a ser necessária se ainda não foi aplicada.

Na tua máquina, na pasta do projeto, executa:

```powershell
.\deploy\vercel\Initialize-Database.ps1
```

O comando pede a ligação direta/unpooled da mesma base/branch Neon usada pelo deploy. No painel Neon, abre o projeto/branch correto, entra em **Connect** e obtém a ligação sem pooling. Mantém o `DATABASE_URL` com pooling no runtime Vercel. O comando aplica as migrações e preserva as respostas existentes; não são aplicadas automaticamente no arranque de produção.

Depois de configurar e publicar:

1. Confirma que `/health` devolve 200.
2. Pesquisa um convidado de teste e introduz um email a que tenhas acesso.
3. Confirma a receção do código, incluindo a pasta de spam, e conclui a resposta.
4. Volta a entrar com o mesmo email e confirma que consegues atualizar a resposta após nova verificação.
5. Confirma que outro email não pode atualizar esse convite e que o primeiro email não pode confirmar outro convite.

## Regras da aplicação

- Um convite corresponde a uma família/grupo em `Families`, incluindo os seus convidados e acompanhantes autorizados.
- O primeiro email é indicado pelo utilizador; não precisa de estar nos campos administrativos do convidado. A verificação comprova o acesso ao email indicado, não a identidade do destinatário do convite em papel.
- O email fica associado ao convite apenas quando a primeira resposta é guardada com sucesso.
- Um email normalizado só pode ficar associado a um convite. Um convite só pode ter um email de confirmação. As restrições únicas da base de dados também impedem associações concorrentes.
- Uma atualização exige um novo código no mesmo email. Não é possível trocar de email na página pública. Maiúsculas/minúsculas e espaços nas extremidades não criam contactos diferentes.
- Os códigos têm 6 algarismos, são guardados como hashes, expiram ao fim de 10 minutos e permitem até 5 tentativas. São de utilização única e um reenvio invalida o código anterior.
- O reenvio tem uma espera mínima de 1 minuto e um máximo de 5 pedidos por email/hora, 20 por IP/hora e 10 por convite/hora. Os limites persistem entre instâncias.
- A sessão verificada dura até 30 minutos e termina quando a resposta é guardada. As páginas privadas não são guardadas em cache.
- As respostas antigas são preservadas. Convites sem email associado passam a ter um na próxima confirmação verificada. Apagar uma resposta não remove a associação do email ao convite.
- Se existirem associações antigas por outro meio, não são convertidas automaticamente nem autorizam acesso por email; a aplicação pede apoio aos noivos. Os respetivos códigos/sessões antigos deixam de dar acesso.

## Resolver problemas de envio

| Sintoma | O que verificar |
| --- | --- |
| Botão de envio indisponível | `RsvpVerification__Email__Enabled=true`, `Host` e `From` no ambiente correto; novo deploy após guardar. |
| Falha de autenticação SMTP | `Username` completo; palavra-passe de aplicação válida em `Password`; verificação em dois passos ativa. |
| Erro depois de alterar a password Google | A Google revoga palavras-passe de aplicação ao alterar a password principal; gera uma nova e atualiza a Vercel. |
| Código não chega | Spam, endereço introduzido e capacidade de envio da conta. O Gmail tem limites diários; um serviço de email transacional pode ser adequado se o volume crescer. |
| Código inválido | Usa o código mais recente, dentro de 10 minutos; depois de várias tentativas, pede um novo. |
| `/health` devolve 503 / tabelas em falta | Aplica a migração na mesma base/branch Neon que a aplicação utiliza. |

Os testes automáticos usam base isolada e envio simulado. O envio real deve ser validado depois de configurares o SMTP; não é enviado email automaticamente durante o build.

## Referências

- [Google: palavras-passe de aplicação](https://support.google.com/accounts/answer/185833?hl=pt)
- [Google: servidor e porta SMTP](https://developers.google.com/workspace/gmail/imap/imap-smtp)
- [Google: limites de envio Gmail](https://support.google.com/mail/answer/22839?hl=en)
- [Vercel: configurar variáveis de ambiente](https://vercel.com/docs/environment-variables/managing-environment-variables)
- [Vercel: variáveis Secret](https://vercel.com/docs/environment-variables/sensitive-environment-variables)
- [Vercel: ligações SMTP](https://vercel.com/kb/guide/serverless-functions-and-smtp)
