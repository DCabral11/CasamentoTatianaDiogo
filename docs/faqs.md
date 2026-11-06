# Perguntas frequentes

No painel de administração, abre **Site e fotografias → Perguntas frequentes**.

- **Adicionar pergunta / Editar:** configurar pergunta, resposta, ordem e publicação. Novas perguntas começam como rascunho.
- **Ordem:** os números menores aparecem primeiro; em caso de empate, a pergunta criada primeiro aparece primeiro. Podem usar-se intervalos de 10 para facilitar inserções.
- **Resposta:** texto simples até 6000 caracteres, com preservação de parágrafos e quebras de linha. HTML é apresentado como texto, sem executar scripts.
- **Pré-visualizar perguntas:** permite rever perguntas publicadas e rascunhos; só administradores têm acesso.
- **Definições da página:** configurar título, introdução, nome no menu, mensagem sem perguntas e ativar/desativar o módulo.
- **Eliminar:** exige confirmação. Gravações e eliminações rejeitam uma versão ultrapassada quando outro administrador alterou o conteúdo entretanto.

O módulo começa desativado. Para o disponibilizar, publica as perguntas desejadas e ativa **Ativar o módulo no site**. O menu passa a apresentar o nome configurado e a página fica disponível em `/Faq`. Desativar o módulo oculta o link e impede o acesso público, preservando o conteúdo na administração.

As perguntas abrem/fecham através de controlos nativos do navegador, utilizáveis por teclado e sem depender de JavaScript. A página usa as cores e o logótipo globais e integra-se na navegação com música do site.

Antes do deployment, aplica a migração PostgreSQL `AddFaqModule` através do [procedimento de inicialização](vercel-deployment.md). A inicialização SQL Server inclui a criação das tabelas. Não são necessárias novas variáveis de ambiente.
