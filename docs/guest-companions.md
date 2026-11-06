# Acompanhantes na ficha do convidado

A gestão está em **Administração → Convidados**. O módulo separado de acompanhantes foi removido.

1. Na ficha da pessoa principal, ativar **Pode levar acompanhante** e guardar.
2. Criar ou editar a ficha do acompanhante, selecionar a mesma família/convite, marcar **É Plus-One** e escolher **Acompanha**.
3. Guardar. A lista de convidados identifica a associação e permite abrir a ficha da pessoa principal.

As duas opções são mutuamente exclusivas. Um Plus-One não pode acompanhar a si próprio, outro Plus-One ou alguém sem permissão para levar acompanhante. As regras são verificadas no servidor e as importações não podem contorná-las. Os ficheiros Excel existentes mantêm as associações; a sua criação/alteração é feita na ficha do convidado.

O Plus-One continua a responder através do RSVP da pessoa que acompanha: não aparece como um convite independente na pesquisa nem é repetido na lista de familiares. A resposta atualiza o seu estado na lista de convidados e nos totais, sem duplicar presenças. Ao apagar a resposta principal na gestão de RSVP, o estado do acompanhante também volta a «Por responder».

Para alterar uma associação que já tenha uma resposta, é necessário remover primeiro essa resposta na gestão de RSVP. Não é permitido retirar a permissão ou mudar a família de uma pessoa com acompanhantes associados sem rever primeiro essas ligações.

## Atualização da base de dados

Aplicar a migração **MoveCompanionsIntoGuests** antes de publicar esta versão. No ambiente habitual Neon/Vercel, executar a partir da raiz do projeto:

```powershell
.\deploy\vercel\Initialize-Database.ps1
```

Usar a ligação direta/unpooled do Neon. Depois, publicar a aplicação na Vercel.

A atualização converte cada acompanhante antigo numa ficha de convidado, associada à mesma pessoa e família, e preserva as referências e os detalhes das respostas existentes. Não tenta juntar pessoas apenas por terem nomes iguais. A tabela antiga permanece como suporte interno do histórico RSVP, sem ecrã de administração próprio. O inicializador também suporta a atualização de instalações SQL Server.

A migração não foi executada na base de produção durante esta alteração. A conversão não tem reversão automática: um rollback requer revisão dos dados e uma cópia anterior da base de dados.
