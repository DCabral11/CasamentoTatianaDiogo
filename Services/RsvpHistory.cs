using System.Text.Json;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Models.Enums;

namespace CasamentoTatianaDiogo.Services;

internal static class RsvpHistory
{
    public static Dictionary<string, string?> Snapshot(RsvpResponse? response, ICollection<PlusOne> companions)
    {
        if (response == null) return [];
        var companion = companions.FirstOrDefault(p => p.Id == response.PlusOneId)
            ?? (response.PlusOne?.Id == response.PlusOneId ? response.PlusOne : null);
        return new()
        {
            ["Presença"] = response.Status switch {
                RsvpStatus.Attending => "Confirmada", RsvpStatus.NotAttending => "Não vai estar presente", _ => "Por responder" },
            ["Restrições alimentares"] = response.DietaryRestrictions,
            ["Mensagem"] = response.Message,
            ["Sugestão musical"] = response.MusicRequest,
            ["Presença de acompanhante"] = response.PlusOneAttending ? "Sim" : "Não",
            ["Acompanhante"] = response.PlusOneId is int id ? $"{companion?.PlusOneDisplayName ?? "Acompanhante"} (#{id})" : null,
            ["Restrições alimentares do acompanhante"] = response.PlusOneDietaryRestrictions,
            ["Mensagem do acompanhante"] = response.PlusOneMessage,
            ["Sugestão musical do acompanhante"] = response.PlusOneMusicRequest
        };
    }

    public static void Record(ApplicationDbContext db, Guest guest, string family, DateTime at, string actor,
        Dictionary<string, string?> before, Dictionary<string, string?> after)
    {
        var changes = before.Keys.Union(after.Keys)
            .Select(field => new RsvpFieldChange(field, before.GetValueOrDefault(field), after.GetValueOrDefault(field)))
            .Where(change => change.Before != change.After).ToList();
        if (changes.Count == 0) return;
        db.RsvpChanges.Add(new RsvpChange {
            GuestId = guest.Id, GuestName = guest.DisplayName, FamilyName = family, ChangedAt = at, Actor = actor,
            Operation = before.Count == 0 ? "Resposta criada" : after.Count == 0 ? "Resposta apagada" : "Resposta alterada",
            ChangesJson = JsonSerializer.Serialize(changes)
        });
    }
}
