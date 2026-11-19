#region References

using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Models.Enums;
using System.ComponentModel.DataAnnotations;

#endregion

namespace CasamentoTatianaDiogo.ViewModels.Admin
{
    #region Seating Plan Records

    public record SeatingGuest(int Id, string Name, string Family, RsvpStatus Status, bool IsPlusOne)
    {
        public string Response => Status switch
        {
            RsvpStatus.Attending => "Presença Confirmada.",
            RsvpStatus.NotAttending => "Não Vai Estar Presente.",
            _ => "Por Responder"
        };
    }

    public record SeatingPlan(List<ReceptionTable> Tables, List<SeatingGuest> Guests)
    {
        public Dictionary<int, SeatingGuest> People { get; } = Guests.ToDictionary(x => x.Id);

        public Dictionary<int, string> Places { get; } = Tables.SelectMany(t => t.Seats.Where(s => s.Assignment != null).Select(s => new { s.Assignment!.GuestId, Place = $"{t.Name} · Lugar {s.Number} (Fila {t.MapRow}, Coluna {t.MapColumn}" })).ToDictionary(x => x.GuestId, x => x.Place);
    }

    public record SeatOption(int Id, string Table, int Number, int Row, int Column);

    public record GuestSeatForm(SeatingGuest Guest, int OriginalSeatId, List<SeatOption> Options);

    #endregion

    public class SeatingTableForm
    {
        public int Id { get; set; }

        public Guid Revision { get; set; }

        [Required(ErrorMessage = "Indica o nome da mesa.")]
        [StringLength(100)]
        public string Name { get; set; } = "";

        [Range(1, 200, ErrorMessage = "Escolhe entre 1 e 200 lugares.")]
        public int Capacity { get; set; } = 10;

        [Range(1, 1000, ErrorMessage = "Escolhe uma fila entre 1 e 1000.")]
        public int MapRow { get; set; } = 1;
        
        [Range(1, 4, ErrorMessage = "Escolhe uma coluna entre 1 e 4.")]
        public int MapColumn { get; set; } = 1;
    }
}