using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Models.Enums;
using ClosedXML.Excel;

namespace CasamentoTatianaDiogo.Services;

public static class RsvpExcelExport
{
    public static byte[] Create(IReadOnlyList<Guest> guests,
        IReadOnlyDictionary<int, RsvpInvitationContact> contacts, WeddingClock clock, int? familyId)
    {
        using var workbook = new XLWorkbook();
        var summary = workbook.AddWorksheet("Resumo");
        var people = workbook.AddWorksheet("Convidados");
        var history = workbook.AddWorksheet("Respostas");
        var companions = workbook.AddWorksheet("Acompanhantes");
        AddRow(people, 1, "ID convidado", "Nome", "Apelido", "Nome apresentado", "ID família", "Família",
            "Referência do convite", "Resposta familiar permitida", "Notas da família", "Email do convidado",
            "Telefone", "Criança", "Acompanhante permitido", "Notas do convidado", "Estado atual",
            "Email verificado do convite", "Contacto associado em (hora local)", "Contacto associado em (UTC)",
            "Registos de resposta", "ID da última resposta", "Última resposta enviada em (hora local)",
            "Última atualização (hora local)", "Restrições alimentares", "Mensagem", "Sugestão musical");
        AddRow(history, 1, "ID resposta", "ID convidado", "Convidado", "ID família", "Família", "Referência do convite",
            "Última resposta deste convidado", "Presença nesta resposta", "Estado atual do convidado",
            "Email do convidado", "Telefone", "Email verificado do convite",
            "Enviada em (hora local)", "Enviada em (UTC)", "Atualizada em (hora local)", "Atualizada em (UTC)",
            "Restrições alimentares", "Mensagem", "Sugestão musical", "Acompanhante presente",
            "ID acompanhante", "Nome do acompanhante", "Apelido do acompanhante", "Nome apresentado do acompanhante",
            "Restrições alimentares do acompanhante", "Mensagem do acompanhante", "Sugestão musical do acompanhante",
            "IP de submissão", "Navegador / dispositivo");
        AddRow(companions, 1, "ID acompanhante", "Nome", "Apelido", "Nome apresentado", "ID convidado principal",
            "Convidado principal", "ID família", "Família", "Referência do convite", "Presença do acompanhante",
            "ID resposta associada", "Restrições alimentares", "Mensagem", "Sugestão musical",
            "Resposta enviada em (hora local)", "Resposta atualizada em (hora local)");

        var peopleRow = 2;
        var historyRow = 2;
        var companionRow = 2;
        var confirmedCompanions = 0;
        foreach (var guest in guests)
        {
            var responses = guest.RsvpResponses.OrderByDescending(r => r.SubmittedAt).ThenByDescending(r => r.Id).ToList();
            var latest = responses.FirstOrDefault();
            var contact = contacts.GetValueOrDefault(guest.FamilyId);
            if (latest is { Status: RsvpStatus.Attending, PlusOneAttending: true })
                confirmedCompanions++;
            AddRow(people, peopleRow++, guest.Id, guest.FirstName, guest.LastName, guest.DisplayName,
                guest.FamilyId, guest.Family?.Name, guest.Family?.GroupCode, YesNo(guest.Family?.AllowGroupRsvp == true),
                guest.Family?.Notes, guest.Email, guest.Phone, YesNo(guest.IsChild), YesNo(guest.AllowPlusOne),
                guest.Notes, Status(guest.CurrentStatus), contact?.NormalizedContact, Local(contact?.ConfirmedAt),
                contact?.ConfirmedAt, responses.Count, latest?.Id, Local(latest?.SubmittedAt), Local(latest?.UpdatedAt),
                latest?.DietaryRestrictions, latest?.Message, latest?.MusicRequest);

            foreach (var response in responses)
            {
                AddRow(history, historyRow++, response.Id, guest.Id, guest.DisplayName, guest.FamilyId,
                    guest.Family?.Name, guest.Family?.GroupCode, YesNo(response.Id == latest?.Id), Status(response.Status),
                    Status(guest.CurrentStatus), guest.Email, guest.Phone, contact?.NormalizedContact,
                    Local(response.SubmittedAt), response.SubmittedAt, Local(response.UpdatedAt), response.UpdatedAt,
                    response.DietaryRestrictions, response.Message, response.MusicRequest, YesNo(response.PlusOneAttending),
                    response.PlusOneId, response.PlusOne?.PlusOneFirstName, response.PlusOne?.PlusOneLastName,
                    response.PlusOne?.PlusOneDisplayName, response.PlusOneDietaryRestrictions,
                    response.PlusOneMessage, response.PlusOneMusicRequest, response.SubmittedFromIp, response.UserAgent);
            }

            foreach (var companion in guest.PlusOnes.OrderBy(p => p.Id))
            {
                var selected = latest?.PlusOneId == companion.Id;
                var presence = latest == null ? "Por responder"
                    : selected && latest.PlusOneAttending && latest.Status == RsvpStatus.Attending ? "Confirmado" : "Não confirmado";
                AddRow(companions, companionRow++, companion.Id, companion.PlusOneFirstName, companion.PlusOneLastName,
                    companion.PlusOneDisplayName, guest.Id, guest.DisplayName, guest.FamilyId, guest.Family?.Name,
                    guest.Family?.GroupCode, presence, latest?.Id, selected ? latest?.PlusOneDietaryRestrictions : null,
                    selected ? latest?.PlusOneMessage : null, selected ? latest?.PlusOneMusicRequest : null,
                    Local(latest?.SubmittedAt), Local(latest?.UpdatedAt));
            }
        }

        AddRow(summary, 1, "Indicador", "Valor");
        AddRow(summary, 2, "Exportado em (hora local)", clock.Now);
        AddRow(summary, 3, "Fuso horário local", clock.TimeZoneId);
        AddRow(summary, 4, "Âmbito", familyId.HasValue ? $"Família ID {familyId}" : "Todos os convites");
        AddRow(summary, 5, "Famílias com convidados", guests.Select(g => g.FamilyId).Distinct().Count());
        AddRow(summary, 6, "Convidados", guests.Count);
        AddRow(summary, 7, "Convidados confirmados", guests.Count(g => g.CurrentStatus == RsvpStatus.Attending));
        AddRow(summary, 8, "Convidados que não vão", guests.Count(g => g.CurrentStatus == RsvpStatus.NotAttending));
        AddRow(summary, 9, "Convidados por responder", guests.Count(g => g.CurrentStatus == RsvpStatus.Pending));
        AddRow(summary, 10, "Crianças confirmadas", guests.Count(g => g.IsChild && g.CurrentStatus == RsvpStatus.Attending));
        AddRow(summary, 11, "Acompanhantes confirmados", confirmedCompanions);
        AddRow(summary, 12, "Total de presenças confirmadas",
            guests.Count(g => g.CurrentStatus == RsvpStatus.Attending) + confirmedCompanions);
        AddRow(summary, 13, "Registos de resposta guardados", historyRow - 2);
        AddRow(summary, 14, "Leitura do histórico",
            "A folha Respostas inclui todos os registos guardados. Ao atualizar uma resposta, a aplicação substitui o conteúdo desse registo. As datas identificam o envio inicial e a última atualização.");
        AddRow(summary, 15, "Dados dos convidados e convites",
            "Nomes, contactos, notas e associações familiares refletem o cadastro atual. Campos vazios correspondem a informação por preencher ou indisponível.");
        AddRow(summary, 16, "Acompanhantes",
            "A folha Acompanhantes apresenta o estado segundo a última resposta do convidado principal. A folha Respostas inclui os detalhes associados a cada registo.");

        Format(summary, "ResumoRsvp", false);
        Format(people, "ConvidadosRsvp", true);
        Format(history, "RespostasRsvp", true);
        Format(companions, "AcompanhantesRsvp", true);
        people.SheetView.FreezeColumns(4);
        history.SheetView.FreezeColumns(3);
        companions.SheetView.FreezeColumns(4);
        summary.Column(1).Width = 38;
        summary.Column(2).Width = 95;
        summary.Rows(14, 16).Height = 60;
        workbook.Properties.Title = "Histórico detalhado de RSVP";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();

        DateTime? Local(DateTime? value) => value.HasValue ? clock.ToLocal(value.Value) : null;
    }

    private static string YesNo(bool value) => value ? "Sim" : "Não";
    private static string Status(RsvpStatus value) => value switch
    {
        RsvpStatus.Attending => "Confirmado",
        RsvpStatus.NotAttending => "Não vai estar presente",
        _ => "Por responder"
    };

    private static void AddRow(IXLWorksheet sheet, int row, params object?[] values)
    {
        for (var column = 0; column < values.Length; column++)
        {
            var value = values[column];
            if (value == null) continue;
            var cell = sheet.Cell(row, column + 1);
            // Assign values as typed data, including text beginning with "=".
            cell.Value = XLCellValue.FromObject(value);
            if (value is DateTime) cell.Style.DateFormat.Format = "dd/mm/yyyy hh:mm:ss";
        }
    }

    private static void Format(IXLWorksheet sheet, string tableName, bool filter)
    {
        var range = sheet.RangeUsed()!;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        range.Style.Alignment.WrapText = true;
        var table = range.CreateTable(tableName);
        table.Theme = XLTableTheme.TableStyleMedium2;
        table.ShowAutoFilter = filter;
        sheet.SheetView.FreezeRows(1);
        sheet.SheetView.FreezeColumns(1);
        sheet.Columns(1, range.ColumnCount()).Width = 26;
        sheet.Row(1).Height = 42;
        if (range.RowCount() > 1) sheet.Rows(2, range.RowCount()).Height = 60;
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.SetRowsToRepeatAtTop(1, 1);
    }
}
