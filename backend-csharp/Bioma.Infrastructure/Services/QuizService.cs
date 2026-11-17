using System.Collections.Concurrent;
using Bioma.Application.DTOs;
using Bioma.Application.Interfaces;
using Bioma.Domain.Entities;
using Bioma.Domain.Enums;
using Bioma.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bioma.Infrastructure.Services;

/// <summary>
/// Épique 4 : Quiz Chronométré (US-22 à US-26).
/// - US-22 : Questions de biologie générées aléatoirement et exclusivement à partir des cartes du catalogue Bioma
/// - US-23 : Chronométrage strict et équitable côté serveur (12 secondes par question)
/// - US-24 : Validation instantanée de la réponse avec explication scientifique détaillée
/// - US-25 : Gain de monnaie virtuelle (crédits) et d'XP en cas de bonne réponse dans les temps
/// - US-26 : Récompense perdue (0 crédit, 0 XP) et signalement d'erreur en cas de dépassement du temps imparti
/// - Quota quotidien : 1 session de 3 questions aléatoires par jour
/// </summary>
public class QuizService : IQuizService
{
    public const int DailyMaxQuestions = 3;
    public const int DefaultTimeLimitSeconds = 12;

    private record ActiveQuizTicket(
        Guid SessionToken,
        Guid PlayerId,
        Guid SpeciesCardId,
        BiologyQuizQuestion QuestionEntity,
        DateTimeOffset StartedAtUtc
    );

    private static readonly ConcurrentDictionary<Guid, ActiveQuizTicket> ActiveTickets = new();
    private static readonly ConcurrentDictionary<Guid, HashSet<Guid>> RecentSpeciesPerPlayer = new();

    private readonly BiomaDbContext _db;
    private readonly Random _rng;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public QuizService(BiomaDbContext db) : this(db, Random.Shared)
    {
    }

    public QuizService(BiomaDbContext db, Random rng)
    {
        _db = db;
        _rng = rng;
    }

    public async Task<QuizDailyStatusDto> GetDailyStatusAsync(Guid playerId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return await GetDailyStatusCoreAsync(playerId, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<QuizDailyStatusDto> GetDailyStatusCoreAsync(Guid playerId, CancellationToken ct)
    {
        var todayStartUtc = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var tomorrowStartUtc = todayStartUtc.AddDays(1);
        var remaining = tomorrowStartUtc - DateTimeOffset.UtcNow;

        var attempts = await _db.QuizAttempts
            .AsNoTracking()
            .Include(a => a.Question)
            .Where(a => a.PlayerId == playerId)
            .OrderByDescending(a => a.AnsweredAt)
            .Take(15)
            .ToListAsync(ct);

        var todayAttempts = attempts
            .Where(a => a.AnsweredAt >= todayStartUtc)
            .ToList();

        int answeredToday = todayAttempts.Count;
        bool canPlayToday = answeredToday < DailyMaxQuestions;
        int correctToday = todayAttempts.Count(a => a.IsCorrect);
        int currencyToday = todayAttempts.Sum(a => a.EarnedCurrency);
        int xpToday = todayAttempts.Sum(a => a.EarnedXp);

        string countdownText = $"{(int)remaining.TotalHours:D2}h {remaining.Minutes:D2}m";

        var historyDtos = attempts
            .Take(6)
            .Select(a => new QuizHistoryItemDto(
                a.Id,
                a.Question?.QuestionText ?? "Question biologique sur le catalogue",
                a.IsCorrect,
                a.IsTimeOut,
                Math.Round(a.ElapsedSeconds, 1),
                a.EarnedCurrency,
                a.EarnedXp,
                a.AnsweredAt))
            .ToList();

        return new QuizDailyStatusDto(
            canPlayToday,
            answeredToday,
            DailyMaxQuestions,
            correctToday,
            currencyToday,
            xpToday,
            countdownText,
            historyDtos
        );
    }

    public async Task<QuizQuestionViewDto?> StartRandomCatalogueQuestionAsync(Guid playerId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var status = await GetDailyStatusCoreAsync(playerId, ct);
            if (!status.CanPlayToday)
            {
                return null;
            }

            // Récupère exclusivement les cartes du catalogue du jeu (dédoublonnées par nom scientifique)
            var rawCards = await _db.Cards
                .AsNoTracking()
                .Where(c => !c.IsHybrid)
                .ToListAsync(ct);

            var catalogue = rawCards
                .GroupBy(c => c.ScientificName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.CreatedAt).First())
                .ToList();

            if (catalogue.Count == 0)
            {
                return null;
            }

            // Évite de poser deux fois une question sur la même espèce dans la même session
            var usedSet = RecentSpeciesPerPlayer.GetOrAdd(playerId, _ => new HashSet<Guid>());
            if (usedSet.Count >= catalogue.Count)
            {
                usedSet.Clear();
            }

            var availableTargets = catalogue.Where(c => !usedSet.Contains(c.Id)).ToList();
            if (availableTargets.Count == 0)
            {
                availableTargets = catalogue;
            }

            var target = availableTargets[_rng.Next(availableTargets.Count)];
            usedSet.Add(target.Id);

            var (category, questionEntity, choices) = BuildRandomQuestionFromCatalogue(target, catalogue);

            var sessionToken = Guid.NewGuid();
            ActiveTickets[sessionToken] = new ActiveQuizTicket(
                sessionToken,
                playerId,
                target.Id,
                questionEntity,
                DateTimeOffset.UtcNow
            );

            return new QuizQuestionViewDto(
                sessionToken,
                questionEntity.Id,
                target.Id,
                target.ImageUrl,
                target.Rarity,
                GachaService.RarityLabel(target.Rarity),
                category,
                questionEntity.QuestionText,
                choices,
                questionEntity.TimeLimitSeconds,
                questionEntity.RewardCurrency,
                questionEntity.RewardXp,
                status.QuestionsAnsweredToday + 1,
                DailyMaxQuestions
            );
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<QuizAnswerResultDto> SubmitAnswerAsync(SubmitQuizAnswerDto request, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var player = await _db.Players
                .Include(p => p.InventoryCards)
                .FirstOrDefaultAsync(p => p.Id == request.PlayerId, ct);

            if (!ActiveTickets.TryRemove(request.SessionToken, out var ticket) || player is null)
            {
                return new QuizAnswerResultDto(
                    false,
                    false,
                    request.SelectedChoiceIndex,
                    0,
                    0,
                    DefaultTimeLimitSeconds,
                    0,
                    0,
                    player?.VirtualCurrency ?? 0,
                    player?.ExperiencePoints ?? 0,
                    player?.Level ?? 1,
                    "Session de question expirée ou déjà validée",
                    "Veuillez lancer la question suivante.",
                    null
                );
            }

            double rawElapsed = Math.Max(0.2, (DateTimeOffset.UtcNow - ticket.StartedAtUtc).TotalSeconds);
            double elapsedSeconds = Math.Round(rawElapsed, 1);
            int limit = ticket.QuestionEntity.TimeLimitSeconds;

            // Vérification stricte du temps côté serveur (avec 1.2s de tolérance réseau) ou timeout client (-1)
            bool isTimeOut = request.SelectedChoiceIndex < 0 || rawElapsed > (limit + 1.2);
            bool isCorrect = !isTimeOut && request.SelectedChoiceIndex == ticket.QuestionEntity.CorrectChoiceIndex;

            int earnedCurrency = isCorrect ? ticket.QuestionEntity.RewardCurrency : 0;
            int earnedXp = isCorrect ? ticket.QuestionEntity.RewardXp : 0;

            // Enregistre la question générée et la tentative dans Supabase PostgreSQL
            _db.QuizQuestions.Add(ticket.QuestionEntity);

            var attempt = new QuizAttemptRecord
            {
                Id = Guid.NewGuid(),
                PlayerId = player.Id,
                QuestionId = ticket.QuestionEntity.Id,
                SelectedChoiceIndex = request.SelectedChoiceIndex,
                ElapsedSeconds = Math.Min(elapsedSeconds, limit + 5.0),
                IsTimeOut = isTimeOut,
                IsCorrect = isCorrect,
                EarnedCurrency = earnedCurrency,
                EarnedXp = earnedXp,
                AnsweredAt = DateTimeOffset.UtcNow
            };
            _db.QuizAttempts.Add(attempt);

            if (isCorrect)
            {
                player.VirtualCurrency += earnedCurrency;
                player.ExperiencePoints += earnedXp;
                player.Level = Math.Max(player.Level, 1 + (player.ExperiencePoints / 100));

                // Les cartes équipées dans le Deck actif gagnent aussi de l'expérience biologique
                foreach (var deckCard in player.InventoryCards.Where(c => c.IsInActiveDeck))
                {
                    deckCard.ExperiencePoints += 15;
                    if (deckCard.ExperiencePoints >= 100 && deckCard.Stage == LifeStage.Juvenile)
                    {
                        deckCard.Stage = LifeStage.Adulte;
                        deckCard.Level += 1;
                        deckCard.CurrentAttack += 6;
                        deckCard.CurrentDefense += 5;
                    }
                }
            }

            player.LastCloudSyncAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);

            var subjectCard = await _db.Cards
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == ticket.SpeciesCardId, ct);

            CardDto? cardDto = null;
            if (subjectCard is not null)
            {
                long occ = GachaService.GetEstimatedNaturalOccurrences(subjectCard.ScientificName, subjectCard.Rarity);
                cardDto = new CardDto(
                    subjectCard.Id,
                    subjectCard.CommonName,
                    subjectCard.ScientificName,
                    subjectCard.Family,
                    subjectCard.SourceApi,
                    subjectCard.ImageUrl,
                    "Catalogue Bioma",
                    subjectCard.CountryOrPlace,
                    subjectCard.ObservedDate,
                    subjectCard.Rarity,
                    GachaService.RarityLabel(subjectCard.Rarity),
                    occ,
                    GachaService.DropRateForRarity(subjectCard.Rarity),
                    subjectCard.BaseAttack,
                    subjectCard.BaseDefense,
                    subjectCard.BaseSpeed
                );
            }

            string statusTitle = isTimeOut
                ? $"Temps dépassé ({elapsedSeconds:F1}s / {limit}s) — Récompense perdue"
                : isCorrect
                    ? $"Bonne réponse en {elapsedSeconds:F1}s (+{earnedCurrency} crédits, +{earnedXp} XP)"
                    : "Mauvaise réponse — Aucune récompense obtenue";

            return new QuizAnswerResultDto(
                isCorrect,
                isTimeOut,
                request.SelectedChoiceIndex,
                ticket.QuestionEntity.CorrectChoiceIndex,
                elapsedSeconds,
                limit,
                earnedCurrency,
                earnedXp,
                player.VirtualCurrency,
                player.ExperiencePoints,
                player.Level,
                statusTitle,
                ticket.QuestionEntity.ScientificExplanation,
                cardDto
            );
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ResetDailyQuotaForDemoAsync(Guid playerId, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var todayStartUtc = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
            var todayAttempts = await _db.QuizAttempts
                .Where(a => a.PlayerId == playerId && a.AnsweredAt >= todayStartUtc)
                .ToListAsync(ct);

            if (todayAttempts.Count > 0)
            {
                _db.QuizAttempts.RemoveRange(todayAttempts);
                await _db.SaveChangesAsync(ct);
            }

            if (RecentSpeciesPerPlayer.TryGetValue(playerId, out var set))
            {
                set.Clear();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private (string Category, BiologyQuizQuestion QuestionEntity, List<string> Choices) BuildRandomQuestionFromCatalogue(
        SpeciesCard target,
        List<SpeciesCard> catalogue)
    {
        long targetOccurrences = GachaService.GetEstimatedNaturalOccurrences(target.ScientificName, target.Rarity);
        string rarityLabel = GachaService.RarityLabel(target.Rarity);
        double dropRate = GachaService.DropRateForRarity(target.Rarity);

        var (rewardCurrency, rewardXp) = target.Rarity switch
        {
            CardRarity.Legendaire => (150, 80),
            CardRarity.Rare       => (110, 55),
            CardRarity.PeuCommune => (80, 40),
            _                     => (60, 30)
        };

        var distractors = catalogue
            .Where(c => !c.ScientificName.Equals(target.ScientificName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(_ => _rng.Next())
            .Take(3)
            .ToList();

        // Choisit aléatoirement le type de question parmi 3 variantes basées sur les cartes du catalogue
        int mode = distractors.Count >= 3 ? _rng.Next(3) : 2;

        string category;
        string questionText;
        string correctText;
        List<string> options;

        if (mode == 0)
        {
            category = "Taxonomie & Nom Scientifique";
            questionText = $"Quel est le nom scientifique exact de l'espèce « {target.CommonName} » présente dans le catalogue Bioma ?";
            correctText = target.ScientificName;
            options = new List<string> { correctText };
            options.AddRange(distractors.Select(d => d.ScientificName));
        }
        else if (mode == 1)
        {
            category = "Identification Visuelle du Catalogue";
            questionText = $"Quelle espèce du catalogue Bioma correspond à cette photographie et au taxon « {target.ScientificName} » ?";
            correctText = target.CommonName;
            options = new List<string> { correctText };
            options.AddRange(distractors.Select(d => d.CommonName));
        }
        else
        {
            category = "Biologie & Rareté dans la Nature";
            questionText = $"Avec environ {targetOccurrences:N0} observations mondiales recensées, quel est le niveau de rareté de « {target.CommonName} » ({target.ScientificName}) dans Bioma ?";
            correctText = rarityLabel;
            options = new List<string> { "Commune", "Peu commune", "Rare", "Légendaire" };
        }

        // Mélange aléatoire des 4 choix (sauf pour l'ordre des raretés où l'on peut aussi mélanger ou garder les 4 choix distincts)
        var shuffled = options
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(_ => _rng.Next())
            .ToList();

        while (shuffled.Count < 4)
        {
            shuffled.Add($"Specimen #{shuffled.Count + 1}");
        }

        int correctIndex = shuffled.FindIndex(c => c.Equals(correctText, StringComparison.OrdinalIgnoreCase));
        if (correctIndex < 0)
        {
            shuffled[0] = correctText;
            correctIndex = 0;
        }

        string explanation = $"{target.CommonName} ({target.ScientificName}) appartient au groupe {target.Family}. " +
                             $"Dans la nature, cette espèce compte environ {targetOccurrences:N0} observations mondiales, " +
                             $"ce qui la classe au rang « {rarityLabel} » ({dropRate} % au tirage) avec ATK {target.BaseAttack} / DEF {target.BaseDefense} / SPD {target.BaseSpeed}.";

        var questionEntity = new BiologyQuizQuestion
        {
            Id = Guid.NewGuid(),
            QuestionText = questionText,
            ChoiceA = shuffled[0],
            ChoiceB = shuffled[1],
            ChoiceC = shuffled[2],
            ChoiceD = shuffled[3],
            CorrectChoiceIndex = correctIndex,
            ScientificExplanation = explanation,
            TimeLimitSeconds = DefaultTimeLimitSeconds,
            RewardCurrency = rewardCurrency,
            RewardXp = rewardXp
        };

        return (category, questionEntity, shuffled);
    }
}
