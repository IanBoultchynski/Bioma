using Bioma.Application.DTOs;
using Bioma.Application.Interfaces;
using Bioma.Domain.Entities;
using Bioma.Domain.Enums;
using Bioma.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bioma.Infrastructure.Services;

/// <summary>
/// Épique 9 : Social &amp; Échanges (US-52 à US-54).
/// - US-52 : demandes d'amis (envoi, acceptation, refus) et suppression d'un ami
/// - US-53 : échange sécurisé d'une carte contre une carte, validé par les deux joueurs
/// - US-54 : cadeau direct d'une carte à un ami
///
/// Sécurité des échanges : rien n'est réservé au moment de la proposition. Tout est revérifié
/// au moment de l'acceptation (amitié, propriétaire et disponibilité des deux cartes), puis les
/// deux cartes changent de propriétaire dans un seul SaveChanges, donc en une seule transaction.
/// </summary>
public class SocialTradeService : ISocialTradeService
{
    private const int HistorySize = 10;

    private readonly BiomaDbContext _db;

    public SocialTradeService(BiomaDbContext db)
    {
        _db = db;
    }

    // ========================================================================
    // VUE D'ENSEMBLE
    // ========================================================================

    public async Task<SocialOverviewDto> GetOverviewAsync(Guid playerId, CancellationToken ct = default)
    {
        var links = await _db.Friendships
            .AsNoTracking()
            .Where(f => f.RequesterPlayerId == playerId || f.AddresseePlayerId == playerId)
            .ToListAsync(ct);

        var offers = await _db.TradeOffers
            .AsNoTracking()
            .Where(t => t.SenderPlayerId == playerId || t.ReceiverPlayerId == playerId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        // Tous les joueurs à afficher (amis, demandes, expéditeurs et destinataires des échanges)
        var playerIds = links.Select(f => OtherPlayerId(f, playerId))
            .Concat(offers.Select(t => t.SenderPlayerId))
            .Concat(offers.Select(t => t.ReceiverPlayerId))
            .Distinct()
            .ToList();

        var players = await _db.Players
            .AsNoTracking()
            .Where(p => playerIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var cardCounts = await _db.PlayerCards
            .AsNoTracking()
            .Where(pc => playerIds.Contains(pc.PlayerId))
            .GroupBy(pc => pc.PlayerId)
            .Select(g => new { PlayerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PlayerId, x => x.Count, ct);

        // Cartes citées dans les échanges (même si elles ont changé de propriétaire depuis)
        var cardIds = offers.Select(t => t.OfferedPlayerCardId)
            .Concat(offers.Where(t => t.RequestedPlayerCardId.HasValue).Select(t => t.RequestedPlayerCardId!.Value))
            .Distinct()
            .ToList();

        var cards = await _db.PlayerCards
            .AsNoTracking()
            .Include(pc => pc.SpeciesCard)
            .Where(pc => cardIds.Contains(pc.Id))
            .ToDictionaryAsync(pc => pc.Id, ct);

        string NameOf(Guid id) => players.TryGetValue(id, out var p) ? p.Username : "Joueur supprimé";
        string AvatarOf(Guid id) => players.TryGetValue(id, out var p) ? p.AvatarUrl : string.Empty;

        TradeCardDto CardOf(Guid id) => cards.TryGetValue(id, out var pc)
            ? ToCardDto(pc)
            : new TradeCardDto(id, "Carte introuvable", string.Empty, RarityLabel(CardRarity.Commune), 1);

        TradeOfferDto ToOfferDto(TradeOffer t) => new(
            t.Id,
            t.SenderPlayerId,
            NameOf(t.SenderPlayerId),
            t.ReceiverPlayerId,
            NameOf(t.ReceiverPlayerId),
            CardOf(t.OfferedPlayerCardId),
            t.RequestedPlayerCardId.HasValue ? CardOf(t.RequestedPlayerCardId.Value) : null,
            !t.RequestedPlayerCardId.HasValue,
            t.ReceiverPlayerId == playerId,
            t.Status,
            t.CreatedAt);

        var friends = links
            .Where(f => f.IsAccepted)
            .Select(f =>
            {
                var otherId = OtherPlayerId(f, playerId);
                return new FriendDto(
                    f.Id,
                    otherId,
                    NameOf(otherId),
                    AvatarOf(otherId),
                    players.TryGetValue(otherId, out var p) ? p.Level : 1,
                    cardCounts.TryGetValue(otherId, out var count) ? count : 0);
            })
            .OrderBy(f => f.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var pendingRequests = links
            .Where(f => !f.IsAccepted)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f =>
            {
                var otherId = OtherPlayerId(f, playerId);
                return new FriendRequestDto(f.Id, otherId, NameOf(otherId), AvatarOf(otherId), f.AddresseePlayerId == playerId);
            })
            .ToList();

        var pending = offers.Where(t => t.Status == TradeStatus.EnAttente).ToList();

        return new SocialOverviewDto(
            friends,
            pendingRequests,
            pending.Where(t => t.ReceiverPlayerId == playerId).Select(ToOfferDto).ToList(),
            pending.Where(t => t.SenderPlayerId == playerId).Select(ToOfferDto).ToList(),
            offers.Where(t => t.Status != TradeStatus.EnAttente).Take(HistorySize).Select(ToOfferDto).ToList());
    }

    // ========================================================================
    // US-52 : AMIS
    // ========================================================================

    public async Task<SocialActionResultDto> SendFriendRequestAsync(Guid playerId, string usernameOrEmail, CancellationToken ct = default)
    {
        var search = (usernameOrEmail ?? string.Empty).Trim().ToLower();
        if (search.Length == 0)
        {
            return Fail("Entrez le pseudo ou l'e-mail de votre ami.");
        }

        var target = await _db.Players
            .FirstOrDefaultAsync(p => p.Username.ToLower() == search || p.Email.ToLower() == search, ct);

        if (target is null)
        {
            return Fail($"Aucun joueur ne correspond à « {usernameOrEmail!.Trim()} ».");
        }

        if (target.Id == playerId)
        {
            return Fail("Vous ne pouvez pas vous ajouter vous-même en ami.");
        }

        var existing = await FindLinkAsync(playerId, target.Id, ct);
        if (existing is not null)
        {
            if (existing.IsAccepted)
            {
                return Fail($"{target.Username} fait déjà partie de vos amis.");
            }

            if (existing.RequesterPlayerId == playerId)
            {
                return Fail($"Vous avez déjà envoyé une demande à {target.Username}.");
            }

            // L'autre joueur nous avait déjà envoyé une demande : on l'accepte directement
            existing.IsAccepted = true;
            await _db.SaveChangesAsync(ct);
            return Ok($"{target.Username} vous avait déjà envoyé une demande : vous êtes maintenant amis.");
        }

        _db.Friendships.Add(new Friendship
        {
            RequesterPlayerId = playerId,
            AddresseePlayerId = target.Id,
            IsAccepted = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        return Ok($"Demande d'ami envoyée à {target.Username}.");
    }

    public async Task<SocialActionResultDto> RespondToFriendRequestAsync(Guid playerId, Guid friendshipId, bool accept, CancellationToken ct = default)
    {
        var request = await _db.Friendships
            .FirstOrDefaultAsync(f => f.Id == friendshipId && f.AddresseePlayerId == playerId && !f.IsAccepted, ct);

        if (request is null)
        {
            return Fail("Cette demande d'ami n'existe plus.");
        }

        string requesterName = await UsernameAsync(request.RequesterPlayerId, ct);

        if (accept)
        {
            request.IsAccepted = true;
            await _db.SaveChangesAsync(ct);
            return Ok($"Vous êtes maintenant amis avec {requesterName}.");
        }

        _db.Friendships.Remove(request);
        await _db.SaveChangesAsync(ct);
        return Ok($"Demande de {requesterName} refusée.");
    }

    public async Task<SocialActionResultDto> RemoveFriendAsync(Guid playerId, Guid otherPlayerId, CancellationToken ct = default)
    {
        var link = await FindLinkAsync(playerId, otherPlayerId, ct);
        if (link is null)
        {
            return Fail("Ce joueur ne fait pas partie de vos amis.");
        }

        bool wasFriend = link.IsAccepted;
        string otherName = await UsernameAsync(otherPlayerId, ct);

        // Les échanges en attente entre les deux joueurs n'ont plus de sens : on les annule
        var pendingBetween = await _db.TradeOffers
            .Where(t => t.Status == TradeStatus.EnAttente &&
                        ((t.SenderPlayerId == playerId && t.ReceiverPlayerId == otherPlayerId) ||
                         (t.SenderPlayerId == otherPlayerId && t.ReceiverPlayerId == playerId)))
            .ToListAsync(ct);

        foreach (var offer in pendingBetween)
        {
            offer.Status = TradeStatus.Annule;
        }

        _db.Friendships.Remove(link);
        await _db.SaveChangesAsync(ct);

        return Ok(wasFriend
            ? $"{otherName} a été retiré de vos amis."
            : $"Demande d'ami à {otherName} annulée.");
    }

    // ========================================================================
    // US-53 & US-54 : CARTES ÉCHANGEABLES
    // ========================================================================

    public async Task<List<TradeCardDto>> GetTradeableCardsAsync(Guid viewerId, Guid ownerId, CancellationToken ct = default)
    {
        // On ne peut voir les cartes échangeables d'un autre joueur que si c'est un ami
        if (viewerId != ownerId && !await AreFriendsAsync(viewerId, ownerId, ct))
        {
            return new List<TradeCardDto>();
        }

        var cards = await _db.PlayerCards
            .AsNoTracking()
            .Include(pc => pc.SpeciesCard)
            .Where(pc => pc.PlayerId == ownerId && pc.State == CardState.Disponible)
            .OrderByDescending(pc => pc.AcquiredAt)
            .ToListAsync(ct);

        return cards
            .OrderByDescending(pc => pc.SpeciesCard?.Rarity ?? CardRarity.Commune)
            .ThenBy(pc => pc.SpeciesCard?.CommonName)
            .Select(ToCardDto)
            .ToList();
    }

    // ========================================================================
    // US-53 : ÉCHANGE SÉCURISÉ
    // ========================================================================

    public async Task<SocialActionResultDto> ProposeTradeAsync(Guid playerId, Guid friendId, Guid offeredPlayerCardId, Guid requestedPlayerCardId, CancellationToken ct = default)
    {
        if (!await AreFriendsAsync(playerId, friendId, ct))
        {
            return Fail("Vous ne pouvez échanger des cartes qu'avec vos amis.");
        }

        var offered = await FindTradeableCardAsync(playerId, offeredPlayerCardId, ct);
        if (offered is null)
        {
            return Fail("Cette carte n'est plus disponible dans votre inventaire.");
        }

        var requested = await FindTradeableCardAsync(friendId, requestedPlayerCardId, ct);
        if (requested is null)
        {
            return Fail("La carte demandée n'est plus disponible chez votre ami.");
        }

        bool alreadyProposed = await _db.TradeOffers.AnyAsync(t =>
            t.Status == TradeStatus.EnAttente &&
            t.SenderPlayerId == playerId &&
            t.OfferedPlayerCardId == offeredPlayerCardId &&
            t.RequestedPlayerCardId == requestedPlayerCardId, ct);

        if (alreadyProposed)
        {
            return Fail("Vous avez déjà proposé cet échange.");
        }

        _db.TradeOffers.Add(new TradeOffer
        {
            SenderPlayerId = playerId,
            ReceiverPlayerId = friendId,
            OfferedPlayerCardId = offered.Id,
            RequestedPlayerCardId = requested.Id,
            Status = TradeStatus.EnAttente,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        string friendName = await UsernameAsync(friendId, ct);
        return Ok($"Proposition envoyée à {friendName} : votre {CardName(offered)} contre son {CardName(requested)}.");
    }

    public async Task<SocialActionResultDto> RespondToTradeAsync(Guid playerId, Guid tradeOfferId, bool accept, CancellationToken ct = default)
    {
        var offer = await _db.TradeOffers.FirstOrDefaultAsync(t =>
            t.Id == tradeOfferId &&
            t.ReceiverPlayerId == playerId &&
            t.Status == TradeStatus.EnAttente &&
            t.RequestedPlayerCardId != null, ct);

        if (offer is null)
        {
            return Fail("Cette proposition d'échange n'existe plus.");
        }

        if (!accept)
        {
            offer.Status = TradeStatus.Refuse;
            await _db.SaveChangesAsync(ct);
            return Ok("Échange refusé.");
        }

        // Tout est revérifié au moment de l'acceptation : c'est ce qui rend l'échange sûr
        var offered = await FindTradeableCardAsync(offer.SenderPlayerId, offer.OfferedPlayerCardId, ct);
        var requested = await FindTradeableCardAsync(offer.ReceiverPlayerId, offer.RequestedPlayerCardId!.Value, ct);
        bool stillFriends = await AreFriendsAsync(offer.SenderPlayerId, offer.ReceiverPlayerId, ct);

        if (offered is null || requested is null || !stillFriends)
        {
            offer.Status = TradeStatus.Annule;
            await _db.SaveChangesAsync(ct);
            return Fail("Échange impossible : une des deux cartes n'est plus disponible. La proposition a été annulée.");
        }

        await TransferCardAsync(offered, offer.ReceiverPlayerId, "Echange", ct);
        await TransferCardAsync(requested, offer.SenderPlayerId, "Echange", ct);
        offer.Status = TradeStatus.Accepte;
        await CancelOtherPendingOffersAsync(offer.Id, new[] { offered.Id, requested.Id }, ct);

        // Un seul SaveChanges : les deux cartes changent de main ensemble, ou pas du tout
        await _db.SaveChangesAsync(ct);

        return Ok($"Échange réussi : vous recevez {CardName(offered)} et donnez {CardName(requested)}.");
    }

    public async Task<SocialActionResultDto> CancelTradeAsync(Guid playerId, Guid tradeOfferId, CancellationToken ct = default)
    {
        var offer = await _db.TradeOffers.FirstOrDefaultAsync(t =>
            t.Id == tradeOfferId &&
            t.SenderPlayerId == playerId &&
            t.Status == TradeStatus.EnAttente, ct);

        if (offer is null)
        {
            return Fail("Cette proposition d'échange n'existe plus.");
        }

        offer.Status = TradeStatus.Annule;
        await _db.SaveChangesAsync(ct);
        return Ok("Proposition d'échange annulée.");
    }

    // ========================================================================
    // US-54 : CADEAU DIRECT
    // ========================================================================

    public async Task<SocialActionResultDto> SendGiftAsync(Guid playerId, Guid friendId, Guid playerCardId, CancellationToken ct = default)
    {
        if (!await AreFriendsAsync(playerId, friendId, ct))
        {
            return Fail("Vous ne pouvez offrir des cartes qu'à vos amis.");
        }

        var card = await FindTradeableCardAsync(playerId, playerCardId, ct);
        if (card is null)
        {
            return Fail("Cette carte n'est plus disponible dans votre inventaire.");
        }

        await TransferCardAsync(card, friendId, "Cadeau", ct);
        await CancelOtherPendingOffersAsync(Guid.Empty, new[] { card.Id }, ct);

        // Le cadeau est enregistré comme un échange déjà accepté, sans carte demandée en retour
        _db.TradeOffers.Add(new TradeOffer
        {
            SenderPlayerId = playerId,
            ReceiverPlayerId = friendId,
            OfferedPlayerCardId = card.Id,
            RequestedPlayerCardId = null,
            Status = TradeStatus.Accepte,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        string friendName = await UsernameAsync(friendId, ct);
        return Ok($"Vous avez offert {CardName(card)} à {friendName}.");
    }

    // ========================================================================
    // OUTILS
    // ========================================================================

    private static Guid OtherPlayerId(Friendship link, Guid playerId) =>
        link.RequesterPlayerId == playerId ? link.AddresseePlayerId : link.RequesterPlayerId;

    private Task<Friendship?> FindLinkAsync(Guid a, Guid b, CancellationToken ct) =>
        _db.Friendships.FirstOrDefaultAsync(f =>
            (f.RequesterPlayerId == a && f.AddresseePlayerId == b) ||
            (f.RequesterPlayerId == b && f.AddresseePlayerId == a), ct);

    private Task<bool> AreFriendsAsync(Guid a, Guid b, CancellationToken ct) =>
        _db.Friendships.AnyAsync(f => f.IsAccepted &&
            ((f.RequesterPlayerId == a && f.AddresseePlayerId == b) ||
             (f.RequesterPlayerId == b && f.AddresseePlayerId == a)), ct);

    /// <summary>
    /// Une carte n'est échangeable que si elle appartient bien au joueur et qu'elle est disponible
    /// (pas en expédition, aux enchères, au repos ou consommée).
    /// </summary>
    private Task<PlayerCard?> FindTradeableCardAsync(Guid ownerId, Guid playerCardId, CancellationToken ct) =>
        _db.PlayerCards
            .Include(pc => pc.SpeciesCard)
            .FirstOrDefaultAsync(pc =>
                pc.Id == playerCardId &&
                pc.PlayerId == ownerId &&
                pc.State == CardState.Disponible, ct);

    /// <summary>
    /// Change le propriétaire d'une carte : elle sort du deck actif et de la Vitrine 3D de l'ancien propriétaire.
    /// </summary>
    private async Task TransferCardAsync(PlayerCard card, Guid newOwnerId, string obtainedVia, CancellationToken ct)
    {
        card.PlayerId = newOwnerId;
        card.IsInActiveDeck = false;
        card.ObtainedVia = obtainedVia;
        card.AcquiredAt = DateTimeOffset.UtcNow;

        var pedestals = await _db.ShowcasePedestals
            .Where(p => p.DisplayedPlayerCardId == card.Id)
            .ToListAsync(ct);

        foreach (var pedestal in pedestals)
        {
            pedestal.DisplayedPlayerCardId = null;
        }
    }

    /// <summary>
    /// Annule les autres propositions en attente qui portent sur des cartes qui viennent de changer de propriétaire.
    /// </summary>
    private async Task CancelOtherPendingOffersAsync(Guid exceptOfferId, Guid[] cardIds, CancellationToken ct)
    {
        var others = await _db.TradeOffers
            .Where(t => t.Status == TradeStatus.EnAttente &&
                        t.Id != exceptOfferId &&
                        (cardIds.Contains(t.OfferedPlayerCardId) ||
                         (t.RequestedPlayerCardId != null && cardIds.Contains(t.RequestedPlayerCardId.Value))))
            .ToListAsync(ct);

        foreach (var other in others)
        {
            other.Status = TradeStatus.Annule;
        }
    }

    private async Task<string> UsernameAsync(Guid playerId, CancellationToken ct) =>
        await _db.Players
            .Where(p => p.Id == playerId)
            .Select(p => p.Username)
            .FirstOrDefaultAsync(ct) ?? "Joueur supprimé";

    private static string CardName(PlayerCard card) =>
        card.SpeciesCard?.CommonName ?? card.CustomNickname ?? "carte";

    private static string RarityLabel(CardRarity rarity) => GachaService.RarityLabel(rarity);

    private static TradeCardDto ToCardDto(PlayerCard pc) => new(
        pc.Id,
        CardName(pc),
        pc.SpeciesCard?.ImageUrl ?? string.Empty,
        RarityLabel(pc.SpeciesCard?.Rarity ?? CardRarity.Commune),
        pc.Level);

    private static SocialActionResultDto Ok(string message) => new(true, message);

    private static SocialActionResultDto Fail(string message) => new(false, message);
}