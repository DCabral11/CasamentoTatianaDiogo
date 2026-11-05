using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CasamentoTatianaDiogo.Data;
using CasamentoTatianaDiogo.Models;
using CasamentoTatianaDiogo.Services.Interfaces;
using CasamentoTatianaDiogo.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CasamentoTatianaDiogo.Services;

public record RsvpVerifiedAccess(Guid ChallengeId, int FamilyId, RsvpVerificationChannel Channel, string Contact);
public record RsvpVerificationTicket(string Token, int GuestId);
public class RsvpVerificationException(string message) : Exception(message);

public class RsvpVerificationService(ApplicationDbContext db, IRsvpVerificationSender sender,
    IDataProtectionProvider protection, TimeProvider time, ILogger<RsvpVerificationService> logger)
{
    public const string ContactUnavailable = "Não é possível usar este contacto para este convite. Usa o contacto da primeira confirmação ou fala com os noivos.";
    public const string InvalidCode = "O código é inválido, expirou ou atingiu o limite de tentativas. Pede um novo código se necessário.";
    private readonly IDataProtector challengeProtector = protection.CreateProtector("Rsvp.Challenge.v1");
    private readonly IDataProtector accessProtector = protection.CreateProtector("Rsvp.Access.v1");
    private readonly PasswordHasher<RsvpVerificationChallenge> hasher = new();
    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public async Task<RsvpContactViewModel?> GetContactFormAsync(int guestId)
    {
        var guest = await db.Guests.AsNoTracking().Where(x => x.Id == guestId)
            .Select(x => new { x.DisplayName, x.FamilyId, FamilyName = x.Family!.Name }).SingleOrDefaultAsync();
        if (guest == null) return null;
        var binding = await db.RsvpInvitationContacts.AsNoTracking().SingleOrDefaultAsync(x => x.FamilyId == guest.FamilyId);
        return new RsvpContactViewModel
        {
            GuestId = guestId, GuestName = guest.DisplayName, FamilyName = guest.FamilyName,
            EmailAvailable = sender.IsAvailable(RsvpVerificationChannel.Email) &&
                (binding == null || binding.Channel == RsvpVerificationChannel.Email),
            HasExistingContact = binding != null,
            RequiresSupport = binding != null && binding.Channel != RsvpVerificationChannel.Email
        };
    }

    public async Task<string> RequestCodeAsync(int guestId, RsvpVerificationChannel channel, string contact, string? ip,
        CancellationToken cancellationToken = default)
    {
        if (channel != RsvpVerificationChannel.Email)
            throw new RsvpVerificationException("A verificação está disponível apenas por email.");
        var normalized = RsvpContact.Normalize(channel, contact);
        if (normalized == null)
            throw new RsvpVerificationException("Indica um endereço de email válido.");
        var familyId = await db.Guests.Where(x => x.Id == guestId).Select(x => (int?)x.FamilyId).SingleOrDefaultAsync(cancellationToken);
        if (!familyId.HasValue) throw new RsvpVerificationException("O convite não foi encontrado.");
        if (!await ContactAllowedAsync(familyId.Value, channel, normalized, cancellationToken))
            throw new RsvpVerificationException(ContactUnavailable);
        if (!sender.IsAvailable(channel))
            throw new RsvpVerificationException("Este meio de envio está indisponível de momento. Tenta mais tarde ou contacta os noivos.");

        // Each check uses an atomic database update/unique insert, including on separate instances.
        if (!await TryUseLimitAsync($"contact:{channel}:{normalized}", 5, TimeSpan.FromMinutes(1), cancellationToken) ||
            !await TryUseLimitAsync($"ip:{ip ?? "unknown"}", 20, TimeSpan.Zero, cancellationToken) ||
            !await TryUseLimitAsync($"family:{familyId}", 10, TimeSpan.Zero, cancellationToken))
            throw new RsvpVerificationException("Aguarda um minuto antes de pedir outro código. Se já fizeste vários pedidos, tenta novamente dentro de uma hora.");

        var now = Now;
        await db.RsvpVerificationChallenges.Where(x => x.FamilyId == familyId.Value && x.Channel == channel &&
            x.NormalizedContact == normalized && x.VerifiedAt == null && x.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ExpiresAt, now), cancellationToken);
        var challenge = new RsvpVerificationChallenge
        {
            GuestId = guestId, FamilyId = familyId.Value, Channel = channel, NormalizedContact = normalized,
            CreatedAt = now, ExpiresAt = now.AddMinutes(10)
        };
        var code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        challenge.CodeHash = hasher.HashPassword(challenge, code);
        db.RsvpVerificationChallenges.Add(challenge);
        await db.SaveChangesAsync(cancellationToken);
        try { await sender.SendAsync(channel, normalized, code, cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            challenge.ExpiresAt = Now;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning("RSVP verification delivery failed. Channel: {Channel}; failure type: {FailureType}", channel, ex.GetType().Name);
            throw new RsvpVerificationException("Não foi possível enviar o código. Aguarda um minuto e tenta novamente ou contacta os noivos.");
        }

        // Keep short-lived verification data bounded without a background process on Vercel.
        await db.RsvpVerificationChallenges.Where(x => x.CreatedAt < now.AddDays(-2)).ExecuteDeleteAsync(cancellationToken);
        await db.RsvpVerificationLimits.Where(x => x.WindowEndsAt < now.AddDays(-1)).ExecuteDeleteAsync(cancellationToken);
        return challengeProtector.Protect(challenge.Id.ToString("N"));
    }

    public async Task<RsvpCodeViewModel?> GetCodeFormAsync(string? token)
    {
        var id = Decode(challengeProtector, token);
        var now = Now;
        var challenge = id.HasValue ? await db.RsvpVerificationChallenges.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == id.Value && x.Channel == RsvpVerificationChannel.Email && x.VerifiedAt == null && x.ExpiresAt > now && x.Attempts < 5) : null;
        return challenge == null ? null : new RsvpCodeViewModel
        {
            GuestId = challenge.GuestId,
            MaskedContact = RsvpContact.Mask(challenge.Channel, challenge.NormalizedContact)
        };
    }

    public async Task<RsvpVerificationTicket> VerifyCodeAsync(string? token, string? code)
    {
        var id = Decode(challengeProtector, token);
        if (!id.HasValue) throw new RsvpVerificationException(InvalidCode);
        var now = Now;
        var attempts = await db.RsvpVerificationChallenges.Where(x => x.Id == id.Value && x.Channel == RsvpVerificationChannel.Email && x.VerifiedAt == null &&
            x.ExpiresAt > now && x.Attempts < 5)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Attempts, x => x.Attempts + 1));
        if (attempts != 1) throw new RsvpVerificationException(InvalidCode);
        var challenge = await db.RsvpVerificationChallenges.AsNoTracking().SingleAsync(x => x.Id == id.Value);
        if (!Regex.IsMatch(code ?? "", "^[0-9]{6}$") || challenge.VerifiedAt != null ||
            hasher.VerifyHashedPassword(challenge, challenge.CodeHash, code!) == PasswordVerificationResult.Failed)
            throw new RsvpVerificationException(InvalidCode);
        if (!await ContactAllowedAsync(challenge.FamilyId, challenge.Channel, challenge.NormalizedContact))
            throw new RsvpVerificationException(ContactUnavailable);
        var consumed = await db.RsvpVerificationChallenges.Where(x => x.Id == id.Value && x.VerifiedAt == null &&
            x.ExpiresAt > now && x.Attempts == challenge.Attempts)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.VerifiedAt, now)
                .SetProperty(x => x.SessionExpiresAt, now.AddMinutes(30)).SetProperty(x => x.CodeHash, ""));
        if (consumed != 1) throw new RsvpVerificationException(InvalidCode);
        return new(accessProtector.Protect(challenge.Id.ToString("N")), challenge.GuestId);
    }

    public async Task<RsvpVerifiedAccess?> GetAccessAsync(string? token, int guestId)
    {
        var id = Decode(accessProtector, token);
        if (!id.HasValue) return null;
        var now = Now;
        var challenge = await db.RsvpVerificationChallenges.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == id.Value && x.Channel == RsvpVerificationChannel.Email && x.VerifiedAt != null && x.SessionExpiresAt > now);
        if (challenge == null || !await db.Guests.AnyAsync(x => x.Id == guestId && x.FamilyId == challenge.FamilyId) ||
            !await ContactAllowedAsync(challenge.FamilyId, challenge.Channel, challenge.NormalizedContact)) return null;
        return new(challenge.Id, challenge.FamilyId, challenge.Channel, challenge.NormalizedContact);
    }

    private async Task<bool> ContactAllowedAsync(int familyId, RsvpVerificationChannel channel, string contact, CancellationToken ct = default) =>
        !await db.RsvpInvitationContacts.AnyAsync(x =>
            (x.FamilyId == familyId && (x.Channel != channel || x.NormalizedContact != contact)) ||
            (x.Channel == channel && x.NormalizedContact == contact && x.FamilyId != familyId), ct);

    private async Task<bool> TryUseLimitAsync(string key, int maximum, TimeSpan cooldown, CancellationToken ct)
    {
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        var now = Now;
        var changed = await db.RsvpVerificationLimits.Where(x => x.Id == id && x.NextAllowedAt <= now &&
            (x.WindowEndsAt <= now || x.Count < maximum))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Count, x => x.WindowEndsAt <= now ? 1 : x.Count + 1)
                .SetProperty(x => x.WindowEndsAt, x => x.WindowEndsAt <= now ? now.AddHours(1) : x.WindowEndsAt)
                .SetProperty(x => x.NextAllowedAt, now.Add(cooldown)), ct);
        if (changed == 1) return true;
        if (await db.RsvpVerificationLimits.AnyAsync(x => x.Id == id, ct)) return false;
        var counter = new RsvpVerificationLimit { Id = id, Count = 1, WindowEndsAt = now.AddHours(1), NextAllowedAt = now.Add(cooldown) };
        db.RsvpVerificationLimits.Add(counter);
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            db.Entry(counter).State = EntityState.Detached;
            return false;
        }
    }

    public static bool IsUniqueViolation(DbUpdateException exception) => exception.InnerException switch
    {
        Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation } => true,
        Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 } => true,
        { } inner when inner.GetType().FullName == "Microsoft.Data.Sqlite.SqliteException" &&
            inner.Message.Contains("UNIQUE constraint failed", StringComparison.Ordinal) => true,
        _ => false
    };

    private static Guid? Decode(IDataProtector protector, string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048) return null;
        try { return Guid.TryParseExact(protector.Unprotect(token), "N", out var id) ? id : null; }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException) { return null; }
    }
}
