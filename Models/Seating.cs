using System.ComponentModel.DataAnnotations;

namespace CasamentoTatianaDiogo.Models;

public class ReceptionTable
{
    public int Id { get; set; }
    [MaxLength(100)] public string Name { get; set; } = "";
    public int MapRow { get; set; }
    public int MapColumn { get; set; }
    public Guid Revision { get; set; }
    public List<TableSeat> Seats { get; set; } = [];
}

public class TableSeat
{
    public int Id { get; set; }
    public int TableId { get; set; }
    public int Number { get; set; }
    public SeatAssignment? Assignment { get; set; }
}

public class SeatAssignment
{
    public int GuestId { get; set; }
    public int SeatId { get; set; }
}
