using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Controllers.Admin;

[Authorize(Roles = "Admin"), AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("Admin/Mesas")]
public class SeatingController(ApplicationDbContext db) : Controller
{
    private const string Views = "~/Views/Admin/Seating/";
    private IQueryable<SeatingGuest> Guests() => db.Guests.AsNoTracking()
        .OrderBy(g => g.DisplayName).ThenBy(g => g.Id)
        .Select(g => new SeatingGuest(g.Id, g.DisplayName, g.Family!.Name, g.CurrentStatus, g.IsPlusOne));

    private async Task<SeatingPlan> PlanAsync()
    {
        var tables = await db.ReceptionTables.AsNoTracking().OrderBy(t => t.MapRow).ThenBy(t => t.MapColumn)
            .Include(t => t.Seats.OrderBy(s => s.Number)).ThenInclude(s => s.Assignment).ToListAsync();
        return new(tables, await Guests().ToListAsync());
    }

    [HttpGet("")]
    public async Task<IActionResult> Index() => View(Views + "Index.cshtml", await PlanAsync());

    [HttpGet("Mapa")]
    public async Task<IActionResult> Map() => View(Views + "Map.cshtml", await PlanAsync());

    [HttpGet("Imprimir")]
    public async Task<IActionResult> Print(bool alphabetical = false)
    {
        ViewBag.Alphabetical = alphabetical;
        return View(Views + "Print.cshtml", await PlanAsync());
    }

    [HttpGet("Mesa/{id:int?}")]
    public async Task<IActionResult> Table(int? id)
    {
        if (id == null)
        {
            var used = await db.ReceptionTables.Select(t => (t.MapRow - 1) * 4 + t.MapColumn - 1).ToListAsync();
            var occupied = used.ToHashSet();
            var position = 0;
            while (occupied.Contains(position)) position++;
            return View(Views + "Table.cshtml", new SeatingTableForm { MapRow = position / 4 + 1, MapColumn = position % 4 + 1 });
        }
        var table = await db.ReceptionTables.Where(t => t.Id == id).Select(t => new SeatingTableForm {
            Id = t.Id, Name = t.Name, Revision = t.Revision, Capacity = t.Seats.Count, MapRow = t.MapRow, MapColumn = t.MapColumn
        }).SingleOrDefaultAsync();
        return table == null ? NotFound() : View(Views + "Table.cshtml", table);
    }

    [HttpPost("Mesa/Guardar")]
    public async Task<IActionResult> SaveTable(SeatingTableForm form)
    {
        if (!ModelState.IsValid) return View(Views + "Table.cshtml", form);
        var table = form.Id == 0 ? new ReceptionTable() : await db.ReceptionTables.Include(t => t.Seats)
            .ThenInclude(s => s.Assignment).SingleOrDefaultAsync(t => t.Id == form.Id);
        if (table == null) return NotFound();
        if (table.Revision != form.Revision) return Back("A mesa mudou entretanto. Revê a configuração atual.");
        if (await db.ReceptionTables.AnyAsync(t => t.Id != form.Id && t.MapRow == form.MapRow && t.MapColumn == form.MapColumn))
            ModelState.AddModelError(nameof(form.MapColumn), "Já existe uma mesa nesta posição do mapa.");
        var excess = table.Seats.Where(s => s.Number > form.Capacity).ToList();
        if (excess.Any(s => s.Assignment != null))
            ModelState.AddModelError(nameof(form.Capacity), "Move os convidados dos lugares que pretendes retirar antes de reduzir a capacidade.");
        if (!ModelState.IsValid) return View(Views + "Table.cshtml", form);

        if (form.Id == 0) db.ReceptionTables.Add(table);
        table.Name = form.Name.Trim();
        table.MapRow = form.MapRow;
        table.MapColumn = form.MapColumn;
        table.Revision = Guid.NewGuid();
        for (var number = table.Seats.Count + 1; number <= form.Capacity; number++)
            table.Seats.Add(new TableSeat { Number = number });
        db.TableSeats.RemoveRange(excess);
        return await SaveAsync();
    }

    [HttpPost("Mesa/{id:int}/Eliminar")]
    public async Task<IActionResult> Delete(int id, Guid revision)
    {
        var table = await db.ReceptionTables.FindAsync(id);
        if (table == null) return NotFound();
        if (table.Revision != revision) return Back("A mesa mudou entretanto. Revê a configuração atual.");
        if (await db.TableSeats.AnyAsync(s => s.TableId == id && s.Assignment != null))
            return Back("Move ou liberta os lugares ocupados antes de eliminar esta mesa.");
        db.ReceptionTables.Remove(table);
        return await SaveAsync();
    }

    [HttpGet("Convidado/{id:int}")]
    public async Task<IActionResult> Guest(int id)
    {
        var person = await db.Guests.Where(g => g.Id == id)
            .Select(g => new SeatingGuest(g.Id, g.DisplayName, g.Family!.Name, g.CurrentStatus, g.IsPlusOne)).SingleOrDefaultAsync();
        if (person == null) return NotFound();
        var original = await db.SeatAssignments.Where(a => a.GuestId == id).Select(a => a.SeatId).SingleOrDefaultAsync();
        var options = await (from seat in db.TableSeats
                             join table in db.ReceptionTables on seat.TableId equals table.Id
                             where seat.Assignment == null || seat.Id == original
                             orderby table.MapRow, table.MapColumn, seat.Number
                             select new SeatOption(seat.Id, table.Name, seat.Number, table.MapRow, table.MapColumn)).ToListAsync();
        return View(Views + "Guest.cshtml", new GuestSeatForm(person, original, options));
    }

    [HttpPost("Convidado/{id:int}/Guardar")]
    public async Task<IActionResult> Assign(int id, int? seatId, int? originalSeatId)
    {
        if (!ModelState.IsValid || seatId is null or < 0 || originalSeatId is null or < 0) return BadRequest();
        if (!await db.Guests.AnyAsync(g => g.Id == id)) return NotFound();
        var assignment = await db.SeatAssignments.FindAsync(id);
        if ((assignment?.SeatId ?? 0) != originalSeatId) return Back("O lugar deste convidado mudou entretanto. Revê a distribuição.");
        if (seatId == originalSeatId) return RedirectToAction(nameof(Index));
        if (seatId == 0) db.SeatAssignments.Remove(assignment!);
        else
        {
            if (!await db.TableSeats.AnyAsync(s => s.Id == seatId && s.Assignment == null))
                return Back("O lugar escolhido já não está disponível. Escolhe outro lugar.");
            if (assignment == null) db.SeatAssignments.Add(new SeatAssignment { GuestId = id, SeatId = seatId.Value });
            else assignment.SeatId = seatId.Value;
        }
        return await SaveAsync();
    }

    private async Task<IActionResult> SaveAsync()
    {
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Back("Não foi possível guardar a alteração. Revê o plano e a disponibilidade dos lugares."); }
        TempData["Success"] = "Plano de mesas atualizado.";
        return RedirectToAction(nameof(Index));
    }

    private IActionResult Back(string message)
    {
        TempData["Error"] = message;
        return RedirectToAction(nameof(Index));
    }
}
