# Idiomas da aplicação

O seletor na navegação permite escolher português (pt-PT), inglês (en-GB) ou francês (fr-FR). A preferência fica guardada durante um ano num cookie essencial. Sem preferência, a aplicação apresenta português. A escolha aplica-se ao site público, à administração, à validação e ao email do código de verificação.

Datas e números seguem o idioma escolhido. Os preços continuam sempre em euros. Os valores técnicos dos formulários, estados e ficheiros Excel mantêm os seus identificadores, independentemente do texto apresentado.

## Conteúdos personalizados

Em **Administração → Traduções**, escolhe English ou Français para traduzir a história, as perguntas frequentes, o programa e os textos editáveis. Os formulários de conteúdos existentes continuam a editar os originais portugueses. Um campo de tradução vazio usa a tradução predefinida, quando existe, ou o original português.

As traduções são guardadas na tabela existente `SiteContents`, em chaves `I18n.{idioma}.{hash}`. Não é necessária uma migração. A página deteta alterações concorrentes para impedir que uma versão antiga substitua traduções mais recentes.

Na confirmação de presença, a mudança de idioma mantém a verificação e a sessão de edição. Com JavaScript e armazenamento de sessão disponíveis, os campos preenchidos são recuperados na página traduzida. O rascunho expira após 30 minutos e não contém tokens de autenticação ou antifalsificação.

## Desenvolvimento

- Usa `T("Texto português")` para textos fixos e `T("Mensagem para {0}", nome)` para mensagens com valores. `TF($"Mensagem para {nome}")` também conserva os argumentos separados da tradução.
- Os três catálogos `Resources/UiResource*.resx` usam como chave `Text` seguido do SHA-256 hexadecimal, em maiúsculas, do texto português codificado em UTF-8 (`LocalizedText.ResourceKey`). Os comentários dos catálogos inglês e francês identificam o texto original. Esta chave evita colisões entre textos que apenas diferem em maiúsculas.
- As mensagens com códigos de erro estão em `Resources/Messages*.resx`. As mensagens usadas pelo JavaScript constam também de `Resources/BrowserMessages.json`; são expostas com codificação JSON segura, sem pedidos de tradução externos.
- Mantém as entradas e os parâmetros `{0}`, `{1}`, etc. coerentes entre catálogos. Não traduzas identificadores, rotas, nomes próprios, moradas ou valores técnicos de formulários.
- Não traduzas os objetos guardados na cache pública. As traduções devem ser aplicadas a cópias por pedido, depois de carregar os originais.

Verificação: `dotnet test tests/CasamentoTatianaDiogo.Tests/CasamentoTatianaDiogo.Tests.csproj --filter FullyQualifiedName~LocalizationTests` e `node --test tests/rsvp-presence.Tests.cjs tests/localization.Tests.cjs`.
