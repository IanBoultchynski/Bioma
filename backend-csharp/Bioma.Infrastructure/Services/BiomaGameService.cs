using Bioma.Application.DTOs;
using Bioma.Application.Interfaces;
using Bioma.Domain.Entities;
using Bioma.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bioma.Infrastructure.Services;

public class BiomaGameService : IBiomaGameService
{
    private readonly BiomaDbContext _db;
    private readonly IPythonAiClient _pythonAi;

    public BiomaGameService(BiomaDbContext db, IPythonAiClient pythonAi)
    {
        _db = db;
        _pythonAi = pythonAi;
    }

    public async Task<PlayerProfileDto> GetPlayerProfileAsync(Guid? playerId = null)
    {
        var pid = playerId ?? BiomaDbSeeder.DefaultPlayerId;
        var player = await _db.Players
            .Include(p => p.Cards)
            .FirstOrDefaultAsync(p => p.Id == pid)
            ?? throw new InvalidOperationException("Joueur introuvable.");

        int distinctSpecies = player.Cards
            .Select(c => c.ScientificSubtitle)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return new PlayerProfileDto(
            player.Id,
            player.Pseudo,
            player.AvatarInitials,
            player.Level,
            player.Experience,
            player.FeuillesCurrency,
            player.EloRating,
            player.Victories,
            player.Defeats,
            distinctSpecies,
            player.Cards.Count
        );
    }

    public async Task<List<CardDto>> GetPlayerCollectionAsync(Guid? playerId = null)
    {
        var pid = playerId ?? BiomaDbSeeder.DefaultPlayerId;
        var cards = await _db.Cards
            .Where(c => c.OwnerId == pid)
            .OrderByDescending(c => c.IsHybrid)
            .ThenByDescending(c => c.Rarity)
            .ThenByDescending(c => c.CurrentXp)
            .ToListAsync();

        return cards.Select(MapToCardDto).ToList();
    }

    public async Task<PackOpeningResultDto> OpenPackAsync(Guid playerId, int packCount = 1)
    {
        var pid = playerId == Guid.Empty ? BiomaDbSeeder.DefaultPlayerId : playerId;
        var player = await _db.Players.FirstAsync(p => p.Id == pid);

        int clampedPacks = Math.Clamp(packCount, 1, 10);
        int totalCost = 180 * clampedPacks; // 180 feuilles par pack "Échos du vivant" (Figma p.15)

        if (player.FeuillesCurrency < totalCost)
        {
            throw new InvalidOperationException($"Monnaie insuffisante ({player.FeuillesCurrency} / {totalCost} feuilles requises).");
        }

        player.FeuillesCurrency -= totalCost;
        var allSpecies = await _db.Species.ToListAsync();
        var rng = Random.Shared;
        var drawnDtos = new List<CardDto>();

        int cardsToDraw = 3 * clampedPacks; // 3 cartes par pack "Échos du vivant"
        for (int i = 0; i < cardsToDraw; i++)
        {
            // Probabilités exactes du Figma p.15 : Commune 65%, Peu commune 28%, Rare 7%
            double roll = rng.NextDouble() * 100.0;
            CardRarity targetRarity = roll <= 7.0 ? CardRarity.Rare
                : roll <= 35.0 ? CardRarity.PeuCommune
                : CardRarity.Commune;

            var pool = allSpecies.Where(s => s.Rarity == targetRarity).ToList();
            var chosen = pool.Count > 0 ? pool[rng.Next(pool.Count)] : allSpecies[rng.Next(allSpecies.Count)];

            var newCard = new Card
            {
                OwnerId = player.Id,
                SpeciesId = chosen.Id,
                SpecimenNumber = rng.Next(25, 199),
                CustomName = chosen.CommonName.Replace(" d'Europe", ""),
                ScientificSubtitle = chosen.ScientificName,
                FamilyName = chosen.Family,
                HabitatName = chosen.Habitat,
                ImageUrl = chosen.ImageUrl,
                Rarity = chosen.Rarity,
                Stage = EvolutionStage.Juvenile,
                CurrentXp = 120,
                MaxXpForEvolution = 1000,
                Hp = chosen.BaseHp,
                Attack = chosen.BaseAttack,
                Defense = chosen.BaseDefense,
                Speed = chosen.BaseSpeed,
                WeightKg = chosen.MaxWeightKg,
                DangerIndex = chosen.DangerIndex
            };

            _db.Cards.Add(newCard);
            drawnDtos.Add(MapToCardDto(newCard));
        }

        await _db.SaveChangesAsync();
        return new PackOpeningResultDto(player.FeuillesCurrency, clampedPacks, drawnDtos);
    }

    public async Task<CardDto> ApplyDailyCareAsync(Guid cardId)
    {
        var card = await _db.Cards.FirstOrDefaultAsync(c => c.Id == cardId)
            ?? throw new InvalidOperationException("Carte introuvable.");

        card.CurrentXp = Math.Min(card.MaxXpForEvolution, card.CurrentXp + 180);
        card.LastCareAppliedAt = DateTimeOffset.UtcNow;

        if (card.CurrentXp >= card.MaxXpForEvolution && card.Stage == EvolutionStage.Juvenile)
        {
            PromoteCardToAdult(card);
        }

        await _db.SaveChangesAsync();
        return MapToCardDto(card);
    }

    public async Task<CardDto> EvolveCardAsync(Guid cardId)
    {
        var card = await _db.Cards.FirstOrDefaultAsync(c => c.Id == cardId)
            ?? throw new InvalidOperationException("Carte introuvable.");

        card.CurrentXp = card.MaxXpForEvolution;
        if (card.Stage == EvolutionStage.Juvenile)
        {
            PromoteCardToAdult(card);
        }
        else
        {
            card.Hp = (int)Math.Round(card.Hp * 1.08);
            card.Attack = (int)Math.Round(card.Attack * 1.08);
        }

        await _db.SaveChangesAsync();
        return MapToCardDto(card);
    }

    private static void PromoteCardToAdult(Card card)
    {
        card.Stage = EvolutionStage.Adulte;
        card.Hp = (int)Math.Round(card.Hp * 1.20);
        card.Attack = (int)Math.Round(card.Attack * 1.20);
        card.Defense = (int)Math.Round(card.Defense * 1.20);
    }

    public async Task<List<BiomeShowcase>> GetPlayerBiomesAsync(Guid? playerId = null)
    {
        var pid = playerId ?? BiomaDbSeeder.DefaultPlayerId;
        return await _db.Biomes.Where(b => b.OwnerId == pid).ToListAsync();
    }

    public async Task<BiomeShowcase> ToggleCardInBiomeAsync(Guid biomeId, Guid cardId)
    {
        var biome = await _db.Biomes.FirstOrDefaultAsync(b => b.Id == biomeId)
            ?? throw new InvalidOperationException("Biome introuvable.");
        var card = await _db.Cards.FirstOrDefaultAsync(c => c.Id == cardId)
            ?? throw new InvalidOperationException("Carte introuvable.");

        if (biome.ExposedCardIds.Contains(cardId))
        {
            biome.ExposedCardIds.Remove(cardId);
            card.DisplayedInBiomeId = null;
        }
        else
        {
            if (biome.ExposedCardIds.Count >= biome.MaxSlots)
            {
                throw new InvalidOperationException($"Ce biome contient déjà le maximum de {biome.MaxSlots} cartes.");
            }
            biome.ExposedCardIds.Add(cardId);
            card.DisplayedInBiomeId = biome.Id;
        }

        await _db.SaveChangesAsync();
        return biome;
    }

    public async Task<CaptureResponseDto> ProcessIrlCaptureAsync(CaptureRequestDto request)
    {
        var pid = request.PlayerId == Guid.Empty ? BiomaDbSeeder.DefaultPlayerId : request.PlayerId;
        var player = await _db.Players.Include(p => p.Cards).FirstAsync(p => p.Id == pid);

        // Appel au Back-Office Python (PyTorch ResNet50 / YOLO)
        var aiResult = await _pythonAi.IdentifyCaptureAsync(request);

        if (aiResult.Status != "RECOGNIZED" || aiResult.Candidates.Count == 0)
        {
            return aiResult;
        }

        var topCandidate = aiResult.Candidates[0];
        var species = await _db.Species.FirstOrDefaultAsync(
            s => s.ScientificName.ToLower() == topCandidate.ScientificName.ToLower())
            ?? await _db.Species.FirstAsync();

        // Scénario alternatif 7a (Fiche Use Case 1) : Doublon -> Conversion en Monnaie virtuelle + XP
        var existingCard = player.Cards.FirstOrDefault(c => c.SpeciesId == species.Id);
        if (existingCard != null)
        {
            int bonusFeuilles = 65;
            int bonusXp = 150;
            player.FeuillesCurrency += bonusFeuilles;
            existingCard.CurrentXp = Math.Min(existingCard.MaxXpForEvolution, existingCard.CurrentXp + bonusXp);
            if (existingCard.CurrentXp >= existingCard.MaxXpForEvolution && existingCard.Stage == EvolutionStage.Juvenile)
            {
                PromoteCardToAdult(existingCard);
            }

            await _db.SaveChangesAsync();
            return new CaptureResponseDto(
                "RECOGNIZED",
                aiResult.Confidence,
                $"{aiResult.Message} (Scénario 7a Doublon : +{bonusFeuilles} feuilles & +{bonusXp} XP attribués à votre spécimen existant !)",
                true,
                bonusFeuilles,
                bonusXp,
                MapToCardDto(existingCard),
                aiResult.Candidates
            );
        }

        var newCard = new Card
        {
            OwnerId = player.Id,
            SpeciesId = species.Id,
            SpecimenNumber = Random.Shared.Next(30, 120),
            CustomName = species.CommonName.Replace(" d'Europe", ""),
            ScientificSubtitle = species.ScientificName,
            FamilyName = species.Family,
            HabitatName = species.Habitat,
            ImageUrl = species.ImageUrl,
            Rarity = species.Rarity,
            Stage = EvolutionStage.Juvenile,
            CurrentXp = 100,
            MaxXpForEvolution = 1000,
            Hp = species.BaseHp,
            Attack = species.BaseAttack,
            Defense = species.BaseDefense,
            Speed = species.BaseSpeed,
            WeightKg = species.MaxWeightKg,
            DangerIndex = species.DangerIndex,
            CapturedAt = DateTimeOffset.UtcNow,
            CaptureLatitude = request.Latitude,
            CaptureLongitude = request.Longitude
        };

        _db.Cards.Add(newCard);
        await _db.SaveChangesAsync();

        return new CaptureResponseDto(
            "RECOGNIZED",
            aiResult.Confidence,
            aiResult.Message,
            false,
            0,
            0,
            MapToCardDto(newCard),
            aiResult.Candidates
        );
    }

    public async Task<FusionLabResponseDto> ExecuteLaboratoryFusionAsync(FusionLabRequestDto request)
    {
        var pid = request.PlayerId == Guid.Empty ? BiomaDbSeeder.DefaultPlayerId : request.PlayerId;
        var player = await _db.Players.FirstAsync(p => p.Id == pid);

        // Exception E2 : Incompatibilité (carte avec elle-même)
        if (request.ParentACardId == request.ParentBCardId)
        {
            return new FusionLabResponseDto(
                false,
                "E2. Incompatibilité : Vous ne pouvez pas fusionner une carte avec elle-même.",
                player.FeuillesCurrency,
                string.Empty,
                null);
        }

        var parentA = await _db.Cards.FirstOrDefaultAsync(c => c.Id == request.ParentACardId && c.OwnerId == pid);
        var parentB = await _db.Cards.FirstOrDefaultAsync(c => c.Id == request.ParentBCardId && c.OwnerId == pid);

        if (parentA == null || parentB == null)
        {
            return new FusionLabResponseDto(false, "Cartes parentes introuvables dans votre inventaire.", player.FeuillesCurrency, string.Empty, null);
        }

        if (parentA.Stage != EvolutionStage.Adulte || parentB.Stage != EvolutionStage.Adulte)
        {
            return new FusionLabResponseDto(
                false,
                "Pré-condition : Les deux cartes parentes doivent être au stade Adulte.",
                player.FeuillesCurrency,
                string.Empty,
                null);
        }

        int fusionCost = 200 + (request.UseCatalystBoost ? 80 : 0);
        if (player.FeuillesCurrency < fusionCost)
        {
            return new FusionLabResponseDto(
                false,
                $"Ressources insuffisantes ({fusionCost} feuilles nécessaires).",
                player.FeuillesCurrency,
                string.Empty,
                null);
        }

        // Étape 1 (Cahier des Charges p.14) : Débit ressources & Verrouillage
        player.FeuillesCurrency -= fusionCost;

        try
        {
            // Étapes 2, 3, 4 : Appel Inter-Services vers le Back-Office Python (IA Générative + Calcul Stats)
            var result = await _pythonAi.FuseCardsAsync(parentA, parentB, request.UseCatalystBoost);

            // Étape 5 : Instanciation de la Carte_Hybride & Mise au repos des parents
            var hybridCard = new Card
            {
                OwnerId = player.Id,
                IsHybrid = true,
                ParentACardId = parentA.Id,
                ParentBCardId = parentB.Id,
                SpecimenNumber = Random.Shared.Next(40, 199),
                CustomName = result.HybridName,
                ScientificSubtitle = "Croisement fictif · Rare",
                FamilyName = "Croisement fictif",
                HabitatName = parentA.HabitatName,
                ImageUrl = result.ImageUrl,
                Rarity = CardRarity.Rare,
                Stage = EvolutionStage.Adulte,
                CurrentXp = 500,
                MaxXpForEvolution = 1000,
                Hp = result.Hp,
                Attack = result.Attack,
                Defense = result.Defense,
                Speed = result.Speed,
                WeightKg = Math.Round((parentA.WeightKg + parentB.WeightKg) / 2.0, 2),
                DangerIndex = Math.Round((parentA.DangerIndex + parentB.DangerIndex) / 2.0, 1)
            };

            parentA.RestingUntil = DateTimeOffset.UtcNow.AddHours(6);
            parentB.RestingUntil = DateTimeOffset.UtcNow.AddHours(6);

            _db.Cards.Add(hybridCard);
            await _db.SaveChangesAsync();

            string boostMsg = request.UseCatalystBoost ? " (Catalyseur Boost +10% appliqué !)" : "";
            return new FusionLabResponseDto(
                true,
                $"Fusion réussie : {hybridCard.CustomName} a rejoint votre collection{boostMsg}.",
                player.FeuillesCurrency,
                result.Prompt,
                MapToCardDto(hybridCard)
            );
        }
        catch
        {
            // Exception E1 (Fiche Use Case 2) : Timeout IA -> Recréditer les ressources du joueur
            player.FeuillesCurrency += fusionCost;
            await _db.SaveChangesAsync();
            return new FusionLabResponseDto(
                false,
                "E1. Le laboratoire est surchargé, vos feuilles vous ont été intégralement recréditées.",
                player.FeuillesCurrency,
                string.Empty,
                null
            );
        }
    }

    public async Task<StartQuizResponseDto> StartTimedQuizAsync(Guid playerId, int questionIndex = 0)
    {
        var pid = playerId == Guid.Empty ? BiomaDbSeeder.DefaultPlayerId : playerId;
        var questions = await _db.QuizQuestions.ToListAsync();
        var question = questions[Math.Abs(questionIndex) % questions.Count];

        var attempt = new ActiveQuizAttempt
        {
            PlayerId = pid,
            QuestionId = question.Id,
            StartedAt = DateTimeOffset.UtcNow,
            IsAnswered = false
        };
        _db.ActiveQuizAttempts.Add(attempt);
        await _db.SaveChangesAsync();

        return new StartQuizResponseDto(
            attempt.Id,
            question.Id,
            question.Theme,
            question.ScientificSubtitle,
            question.ImageUrl,
            question.QuestionText,
            question.Options,
            question.TimeLimitSeconds,
            attempt.StartedAt
        );
    }

    public async Task<SubmitQuizAnswerResponseDto> SubmitQuizAnswerAsync(SubmitQuizAnswerRequestDto request)
    {
        var attempt = await _db.ActiveQuizAttempts.FirstOrDefaultAsync(a => a.Id == request.AttemptId);
        var question = await _db.QuizQuestions.FirstOrDefaultAsync(q => q.Id == request.QuestionId)
            ?? throw new InvalidOperationException("Question introuvable.");
        var player = await _db.Players.FirstAsync(p => p.Id == (request.PlayerId == Guid.Empty ? BiomaDbSeeder.DefaultPlayerId : request.PlayerId));

        // Diagramme de séquence p.7 : Le serveur calcule lui-même le temps écoulé (ignore le timer client)
        DateTimeOffset startedAt = attempt?.StartedAt ?? DateTimeOffset.UtcNow.AddSeconds(-4);
        double elapsedSeconds = Math.Round((DateTimeOffset.UtcNow - startedAt).TotalSeconds, 2);

        if (attempt != null)
        {
            attempt.IsAnswered = true;
        }

        // Tolérance ping incluse (11.5s côté serveur pour 10s affichées)
        if (elapsedSeconds > question.TimeLimitSeconds + 1.5)
        {
            await _db.SaveChangesAsync();
            return new SubmitQuizAnswerResponseDto(
                false,
                true,
                elapsedSeconds,
                $"Temps écoulé ({elapsedSeconds:F1}s > {question.TimeLimitSeconds}s) ! Trop tard !",
                0,
                0,
                question.CorrectOptionIndex
            );
        }

        if (request.SelectedOptionIndex == question.CorrectOptionIndex)
        {
            player.Experience += question.RewardXp;
            player.FeuillesCurrency += question.RewardFeuilles;
            await _db.SaveChangesAsync();

            return new SubmitQuizAnswerResponseDto(
                true,
                false,
                elapsedSeconds,
                $"Bonne réponse en {elapsedSeconds:F1}s ! +{question.RewardXp} XP et +{question.RewardFeuilles} feuilles.",
                question.RewardXp,
                question.RewardFeuilles,
                question.CorrectOptionIndex
            );
        }

        await _db.SaveChangesAsync();
        return new SubmitQuizAnswerResponseDto(
            false,
            false,
            elapsedSeconds,
            "Faux ! Ce n'était pas la bonne espèce.",
            0,
            0,
            question.CorrectOptionIndex
        );
    }

    public async Task<List<GeoHotspotZone>> GetHotspotsForSpeciesAsync(string scientificName, int? month = null)
    {
        // Appel du moteur DBSCAN Python
        var pythonHotspots = await _pythonAi.GetExpeditionHotspotsAsync(scientificName, month);
        if (pythonHotspots.Count > 0)
        {
            return pythonHotspots;
        }

        return await _db.HotspotZones
            .Where(z => string.IsNullOrEmpty(scientificName) || z.TargetScientificName.ToLower().Contains(scientificName.ToLower()))
            .ToListAsync();
    }

    public async Task<List<Expedition>> GetPlayerExpeditionsAsync(Guid? playerId = null)
    {
        var pid = playerId ?? BiomaDbSeeder.DefaultPlayerId;
        return await _db.Expeditions
            .Where(e => e.PlayerId == pid && e.Status == ExpeditionStatus.InProgress)
            .OrderByDescending(e => e.StartedAt)
            .ToListAsync();
    }

    public async Task<Expedition> LaunchExpeditionAsync(LaunchExpeditionRequestDto request)
    {
        var pid = request.PlayerId == Guid.Empty ? BiomaDbSeeder.DefaultPlayerId : request.PlayerId;
        var card = await _db.Cards.FirstOrDefaultAsync(c => c.Id == request.CompanionCardId && c.OwnerId == pid)
            ?? await _db.Cards.FirstAsync(c => c.OwnerId == pid);

        card.IsInExpedition = true;
        int hours = Math.Clamp(request.DurationHours, 24, 72);

        var expedition = new Expedition
        {
            PlayerId = pid,
            CompanionCardId = card.Id,
            CompanionName = $"{card.CustomName} #{card.SpecimenNumber}",
            TargetSpeciesName = request.TargetSpeciesName,
            RegionName = request.RegionName,
            StartedAt = DateTimeOffset.UtcNow,
            EndsAt = DateTimeOffset.UtcNow.AddHours(hours),
            DurationHours = hours,
            Status = ExpeditionStatus.InProgress,
            RewardFeuilles = 140,
            RewardXp = 200
        };

        _db.Expeditions.Add(expedition);
        await _db.SaveChangesAsync();
        return expedition;
    }

    public async Task<bool> CancelExpeditionAsync(Guid expeditionId)
    {
        var exp = await _db.Expeditions.FirstOrDefaultAsync(e => e.Id == expeditionId);
        if (exp == null) return false;

        exp.Status = ExpeditionStatus.Cancelled;
        var card = await _db.Cards.FirstOrDefaultAsync(c => c.Id == exp.CompanionCardId);
        if (card != null)
        {
            card.IsInExpedition = false;
        }

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<AuctionListingDto>> GetActiveAuctionsAsync()
    {
        var listings = await _db.AuctionListings
            .Include(a => a.Card)
            .Where(a => a.Status == AuctionStatus.Active)
            .OrderBy(a => a.EndsAt)
            .ToListAsync();

        return listings.Select(MapToAuctionDto).ToList();
    }

    public async Task<AuctionListingDto> CreateAuctionAsync(CreateAuctionRequestDto request)
    {
        var pid = request.SellerId == Guid.Empty ? BiomaDbSeeder.DefaultPlayerId : request.SellerId;
        var seller = await _db.Players.FirstAsync(p => p.Id == pid);
        var card = await _db.Cards.FirstOrDefaultAsync(c => c.Id == request.CardId && c.OwnerId == pid)
            ?? throw new InvalidOperationException("Carte introuvable.");

        card.IsLockedForAuction = true;

        var listing = new AuctionListing
        {
            LotNumber = Random.Shared.Next(207, 499),
            CardId = card.Id,
            Card = card,
            SellerId = seller.Id,
            SellerPseudo = seller.Pseudo,
            StartingPrice = Math.Max(20, request.StartingPrice),
            CurrentBid = Math.Max(20, request.StartingPrice),
            BidCount = 0,
            StartedAt = DateTimeOffset.UtcNow,
            EndsAt = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(request.DurationMinutes, 10, 1440)),
            Status = AuctionStatus.Active
        };

        _db.AuctionListings.Add(listing);
        await _db.SaveChangesAsync();
        return MapToAuctionDto(listing);
    }

    public async Task<(AuctionListingDto Listing, Guid? RefundedBidderId, int RefundedAmount)> PlaceBidAsync(PlaceBidRequestDto request)
    {
        var listing = await _db.AuctionListings
            .Include(a => a.Card)
            .FirstOrDefaultAsync(a => a.Id == request.AuctionListingId && a.Status == AuctionStatus.Active)
            ?? throw new InvalidOperationException("Enchère introuvable ou terminée.");

        if (request.BidAmount <= listing.CurrentBid)
        {
            throw new InvalidOperationException($"Votre offre ({request.BidAmount} feuilles) doit être supérieure à l'enchère actuelle ({listing.CurrentBid} feuilles).");
        }

        var bidderId = request.BidderId == Guid.Empty ? BiomaDbSeeder.DefaultPlayerId : request.BidderId;
        var bidder = await _db.Players.FirstOrDefaultAsync(p => p.Id == bidderId)
            ?? await _db.Players.FirstAsync(p => p.Id == BiomaDbSeeder.DefaultPlayerId);

        if (bidder.FeuillesCurrency < request.BidAmount)
        {
            throw new InvalidOperationException("Solde de feuilles insuffisant pour cette surenchère.");
        }

        // User Story #51 : Remboursement immédiat de l'enchérisseur précédent
        Guid? refundedBidderId = listing.HighestBidderId;
        int refundedAmount = listing.CurrentBid;
        if (refundedBidderId.HasValue)
        {
            var previousBidder = await _db.Players.FirstOrDefaultAsync(p => p.Id == refundedBidderId.Value);
            if (previousBidder != null)
            {
                previousBidder.FeuillesCurrency += refundedAmount;
            }
        }

        bidder.FeuillesCurrency -= request.BidAmount;
        listing.HighestBidderId = bidder.Id;
        listing.HighestBidderPseudo = string.IsNullOrWhiteSpace(request.BidderPseudo) ? bidder.Pseudo : request.BidderPseudo;
        listing.CurrentBid = request.BidAmount;
        listing.BidCount += 1;

        _db.AuctionBids.Add(new AuctionBid
        {
            AuctionListingId = listing.Id,
            BidderId = bidder.Id,
            BidderPseudo = listing.HighestBidderPseudo,
            Amount = request.BidAmount,
            BidAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync();
        return (MapToAuctionDto(listing), refundedBidderId, refundedAmount);
    }

    public async Task<CombatResultDto> ResolveCombat1v1Async(CombatRequestDto request)
    {
        var pid = request.AttackerId == Guid.Empty ? BiomaDbSeeder.DefaultPlayerId : request.AttackerId;
        var attacker = await _db.Players.Include(p => p.Cards).FirstAsync(p => p.Id == pid);

        var activeCards = attacker.Cards.Take(3).ToList();
        string biome = string.IsNullOrWhiteSpace(request.ArenaBiome) ? "Forêt" : request.ArenaBiome;

        // Formule exacte du Diagramme de Séquence p.7 :
        // Puissance = PoidsScore * 0.4 + DangerScore * 0.6 + Multiplicateur de Biome
        double attackerPower = 0;
        foreach (var c in activeCards)
        {
            double weightScore = Math.Min(100.0, Math.Log10(Math.Max(0.05, c.WeightKg) * 20.0 + 1.0) * 25.0);
            double dangerScore = c.DangerIndex * 10.0;
            double biomeBonus = c.HabitatName.Contains(biome, StringComparison.OrdinalIgnoreCase) ? 18.0 : 0.0;
            attackerPower += (weightScore * 0.4) + (dangerScore * 0.6) + biomeBonus;
        }
        attackerPower = Math.Round(attackerPower, 1);

        // Score défenseur simulé autour du niveau Elo
        double defenderPower = Math.Round(135.0 + (Random.Shared.NextDouble() * 45.0), 1);
        bool won = attackerPower >= defenderPower;

        // Diagramme de séquence p.7 : ajusterElo(idAttaquant, +25) ou -15
        int eloDelta = won ? 25 : -15;
        attacker.EloRating += eloDelta;
        if (won)
        {
            attacker.Victories += 1;
            attacker.FeuillesCurrency += 55;
        }
        else
        {
            attacker.Defeats += 1;
        }

        string breakdown = $"Puissance = (Poids×0.4 + Danger×0.6 + Bonus Biome {biome}) = {attackerPower:F1} vs {defenderPower:F1}";

        var combat = new Combat1v1
        {
            AttackerId = attacker.Id,
            AttackerPseudo = attacker.Pseudo,
            DefenderId = Guid.NewGuid(),
            DefenderPseudo = "Explorateur Rival",
            ArenaBiome = biome,
            AttackerScore = attackerPower,
            DefenderScore = defenderPower,
            AttackerWon = won,
            EloDelta = eloDelta,
            CombatSummary = breakdown,
            FoughtAt = DateTimeOffset.UtcNow
        };

        _db.Combats.Add(combat);
        await _db.SaveChangesAsync();

        return new CombatResultDto(
            combat.Id,
            combat.AttackerPseudo,
            combat.DefenderPseudo,
            combat.ArenaBiome,
            combat.AttackerScore,
            combat.DefenderScore,
            combat.AttackerWon,
            combat.EloDelta,
            attacker.EloRating,
            breakdown
        );
    }

    public async Task<List<Combat1v1>> GetCombatHistoryAsync(Guid? playerId = null)
    {
        var pid = playerId ?? BiomaDbSeeder.DefaultPlayerId;
        return await _db.Combats
            .Where(c => c.AttackerId == pid)
            .OrderByDescending(c => c.FoughtAt)
            .Take(10)
            .ToListAsync();
    }

    private static CardDto MapToCardDto(Card c)
    {
        string rarityLabel = c.Rarity switch
        {
            CardRarity.Commune => "Commune",
            CardRarity.PeuCommune => "Peu commune",
            CardRarity.Rare => "Rare",
            CardRarity.Epique => "Épique",
            CardRarity.Legendaire => "Légendaire",
            _ => "Commune"
        };

        return new CardDto(
            c.Id,
            c.SpecimenNumber,
            c.CustomName,
            c.ScientificSubtitle,
            c.FamilyName,
            c.HabitatName,
            c.ImageUrl,
            rarityLabel,
            c.Rarity,
            c.Stage == EvolutionStage.Adulte ? "Adulte" : "Juvénile",
            c.Stage == EvolutionStage.Adulte,
            c.IsHybrid,
            c.CurrentXp,
            c.MaxXpForEvolution,
            c.Hp,
            c.Attack,
            c.Defense,
            c.Speed,
            c.WeightKg,
            c.DangerIndex,
            c.IsLockedForAuction,
            c.IsInExpedition,
            c.DisplayedInBiomeId
        );
    }

    private static AuctionListingDto MapToAuctionDto(AuctionListing a)
    {
        var remaining = a.EndsAt - DateTimeOffset.UtcNow;
        string timeText = remaining.TotalMinutes <= 0
            ? "Terminée"
            : remaining.TotalHours >= 1
                ? $"{(int)remaining.TotalHours} h {remaining.Minutes:D2} min"
                : $"{Math.Max(1, (int)remaining.TotalMinutes)} min";

        string rarityBadge = a.Card?.IsHybrid == true ? "Hybride"
            : a.Card?.Rarity == CardRarity.Rare ? "Rare"
            : a.Card?.Rarity == CardRarity.PeuCommune ? "Peu commune"
            : "Commune";

        return new AuctionListingDto(
            a.Id,
            a.LotNumber,
            a.CardId,
            a.Card?.CustomName ?? "Spécimen",
            a.Card?.ScientificSubtitle ?? "",
            a.Card?.FamilyName ?? "Animalia",
            rarityBadge,
            a.Card?.ImageUrl ?? "",
            a.Card?.IsHybrid ?? false,
            a.SellerId,
            a.SellerPseudo,
            a.HighestBidderId,
            a.HighestBidderPseudo,
            a.CurrentBid,
            a.BidCount,
            a.EndsAt,
            timeText
        );
    }
}
