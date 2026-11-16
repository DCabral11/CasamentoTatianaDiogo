using System.Net;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasamentoTatianaDiogo.Tests;

public class SeatingTests
{
    [Fact]
    public async Task PlanProtectsOccupiedSeatsAndSupportsMovesMapAndPrinting()
    {
        using var app = new HttpTests.TestApplication();
        using var client = app.Client();
        const string root = "/Admin/Mesas";
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(root + "/Imprimir")).StatusCode);
        await ExcelImportTests.Login(app, client);
        var token = await HttpTests.AntiforgeryToken(client, root + "/Mesa");
        Task<HttpResponseMessage> Post(string path, params (string Key, string Value)[] fields) => client.PostAsync(root + path,
            new FormUrlEncodedContent(fields.Append(("__RequestVerificationToken", token)).ToDictionary(f => f.Item1, f => f.Item2)));
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var family = new Family { Name = "Silva" };
        var guest = new Guest { DisplayName = "Zita", Family = family };
        var companion = new Guest { DisplayName = "Ana", Family = family, IsPlusOne = true, MainGuest = guest };
        db.Guests.AddRange(guest, companion);
        await db.SaveChangesAsync();
        var create = await Post("/Mesa/Guardar", ("Name", "Oliveira"), ("Capacity", "2"), ("MapRow", "1"), ("MapColumn", "1"));
        Assert.Equal(HttpStatusCode.Redirect, create.StatusCode);
        var table = await db.ReceptionTables.AsNoTracking().SingleAsync();
        var seats = await db.TableSeats.OrderBy(s => s.Number).ToListAsync();
        Assert.Equal(2, seats.Count);
        await Post("/Mesa/Guardar", ("Name", "Duplicada"), ("Capacity", "2"), ("MapRow", "1"), ("MapColumn", "1"));
        Assert.Single(await db.ReceptionTables.ToListAsync());
        Task<HttpResponseMessage> Assign(int id, int original, int next) => Post($"/Convidado/{id}/Guardar",
            ("originalSeatId", original.ToString()), ("seatId", next.ToString()));
        Assert.Contains("Oliveira", await client.GetStringAsync(root + $"/Convidado/{companion.Id}"));
        await Assign(companion.Id, 0, seats[1].Id);
        await Assign(guest.Id, 0, seats[1].Id);
        Assert.Equal(companion.Id, await db.SeatAssignments.Select(a => a.GuestId).SingleAsync());
        Task<HttpResponseMessage> Resize(int capacity) => Post("/Mesa/Guardar", ("Id", table.Id.ToString()), ("Revision", table.Revision.ToString()),
            ("Name", table.Name), ("Capacity", capacity.ToString()), ("MapRow", "2"), ("MapColumn", "3"));
        var blocked = await Resize(1);
        Assert.Contains("lugares que pretendes retirar", await blocked.Content.ReadAsStringAsync());
        await Post($"/Mesa/{table.Id}/Eliminar", ("revision", table.Revision.ToString()));
        Assert.Equal(2, await db.TableSeats.CountAsync());
        await Assign(companion.Id, seats[1].Id, seats[0].Id);
        await Assign(companion.Id, seats[1].Id, 0); // another editor still has the old seat
        Assert.Equal(seats[0].Id, await db.SeatAssignments.Select(a => a.SeatId).SingleAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await Resize(1)).StatusCode);
        var map = WebUtility.HtmlDecode(await client.GetStringAsync(root + "/Mapa"));
        Assert.Contains("Fila 2", map);
        Assert.Contains("Coluna 3", map);
        Assert.Contains("Completa", map);
        var byTable = await client.GetStringAsync(root + "/Imprimir");
        Assert.Contains("Oliveira", byTable);
        Assert.Contains("Ana", byTable);
        Assert.DoesNotContain("Zita", byTable);
        var list = WebUtility.HtmlDecode(await client.GetStringAsync(root + "/Imprimir?alphabetical=true"));
        Assert.True(list.IndexOf(">Ana<", StringComparison.Ordinal) < list.IndexOf(">Zita<", StringComparison.Ordinal));
        Assert.Contains("Sem lugar", list);
        Assert.Contains("Oliveira", await client.GetStringAsync(root));
        await Assign(companion.Id, seats[0].Id, 0);
        var revision = await db.ReceptionTables.Select(t => t.Revision).SingleAsync();
        await Post($"/Mesa/{table.Id}/Eliminar", ("revision", revision.ToString()));
        Assert.Empty(await db.TableSeats.ToListAsync());
        Assert.Equal(2, await db.Guests.CountAsync());
    }
}
