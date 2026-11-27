using Bioma.Domain.Enums;

namespace Bioma.Application.DTOs;

/// <summary>
/// Un ami du joueur (demande acceptée) (US-52).
/// </summary>
public record FriendDto(
    Guid FriendshipId,
    Guid PlayerId,
    string Username,
    string AvatarUrl,
    int Level,
    int TotalCards
);

/// <summary>
/// Une demande d'ami en attente, reçue ou envoyée par le joueur (US-52).
/// </summary>
public record FriendRequestDto(
    Guid FriendshipId,
    Guid PlayerId,
    string Username,
    string AvatarUrl,
    bool IsIncoming
);

/// <summary>
/// Carte affichée dans un échange ou un cadeau (US-53, US-54).
/// </summary>
public record TradeCardDto(
    Guid PlayerCardId,
    string CommonName,
    string ImageUrl,
    string Rarity,
    int Level
);

/// <summary>
/// Proposition d'échange ou cadeau entre deux amis (US-53, US-54).
/// RequestedCard est null pour un cadeau.
/// </summary>
public record TradeOfferDto(
    Guid Id,
    Guid SenderPlayerId,
    string SenderUsername,
    Guid ReceiverPlayerId,
    string ReceiverUsername,
    TradeCardDto OfferedCard,
    TradeCardDto? RequestedCard,
    bool IsGift,
    bool IsIncoming,
    TradeStatus Status,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Tout ce qu'affiche la page Social : amis, demandes, échanges en cours et historique (US-52 à US-54).
/// </summary>
public record SocialOverviewDto(
    List<FriendDto> Friends,
    List<FriendRequestDto> PendingRequests,
    List<TradeOfferDto> IncomingOffers,
    List<TradeOfferDto> OutgoingOffers,
    List<TradeOfferDto> History
);

/// <summary>
/// Résultat d'une action sociale (demande d'ami, échange, cadeau).
/// </summary>
public record SocialActionResultDto(
    bool Success,
    string Message
);