# Importação de famílias e convidados

Os dois imports da administração usam o mesmo processo:

1. Descarregar o modelo e carregar um ficheiro `.xlsx` (até 5 MB e 500 linhas, sem fórmulas).
2. Rever os totais de registos a criar, atualizar, sem alterações e com erros. Abrir cada linha para comparar todos os valores atuais com os propostos.
3. Se necessário, usar **Corrigir dados**. A opção **Validar e rever pré-visualização** guarda apenas a revisão temporária e mostra uma nova comparação.
4. Escolher **Confirmar importação** para aplicar o lote inteiro. Enquanto houver erros, nenhuma linha é importada. **Cancelar importação** elimina apenas a revisão temporária.

## Identificar os registos a atualizar

- **Famílias:** o ID identifica o registo; sem ID, uma referência existente e única identifica a família a atualizar. Sem correspondência, é criado um registo novo. Referências repetidas, conflitos entre ID e referência e nomes de famílias já existentes sem identificação inequívoca são assinalados.
- **Convidados:** é necessário indicar o ID existente para atualizar. Sem ID, é criado um convidado novo, exceto quando o nome e apelido já existem na mesma família: nesse caso a revisão pede o ID, evitando escolher automaticamente entre homónimos.
- Os IDs aparecem nas listas de famílias e convidados da administração. Os modelos antigos sem a coluna ID continuam aceites; pode adicionar-se essa coluna para atualizar convidados.
- Campos opcionais vazios **limpam o valor anterior**; o resultado é indicado na comparação. Os booleanos vazios mantêm os valores predefinidos do modelo (resposta familiar: Sim; acompanhante e criança: Não).
- Só são alterados os campos presentes no modelo. Respostas RSVP, acompanhantes, contacto de verificação e enquadramento dos avatares existentes são preservados. A mudança de família de um convidado deve ser feita na sua ficha, fora do import, para permitir rever as ligações ao convite.

## Confirmação e validade

A revisão pertence ao administrador que a criou e expira ao fim de 30 minutos. É guardada na base de dados para funcionar entre instâncias da Vercel. Dados corrigidos exigem nova pré-visualização; separadores antigos não podem confirmar uma revisão ultrapassada.

No momento da confirmação, os dados são novamente validados dentro de uma transação. Se os registos relevantes tiverem sido alterados entretanto, a importação é interrompida e é apresentada uma nova comparação para confirmar. Cada revisão só pode ser importada uma vez. Revisões expiradas são eliminadas ao iniciar novos imports.

## Deployment

Aplicar a migração PostgreSQL `AddAdminImportDrafts` antes de publicar a versão, seguindo [o procedimento de inicialização](vercel-deployment.md). Para SQL Server, a inicialização inclui a criação idempotente da tabela. Não são necessárias novas variáveis de ambiente.
