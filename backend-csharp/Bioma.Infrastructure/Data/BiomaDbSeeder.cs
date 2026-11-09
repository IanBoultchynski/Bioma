using Bioma.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bioma.Infrastructure.Data;

public static class BiomaDbSeeder
{
    public static readonly Guid DefaultPlayerId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    public static async Task SeedAsync(BiomaDbContext db)
    {
        if (await db.Players.AnyAsync())
        {
            return;
        }

        // 1. Joueur principal ("Camille" avec 1240 feuilles, comme sur les écrans Figma)
        var camille = new Player
        {
            Id = DefaultPlayerId,
            Pseudo = "Camille",
            Email = "camille@bioma.eco",
            AvatarInitials = "C",
            Level = 7,
            Experience = 2450,
            FeuillesCurrency = 1240,
            EloRating = 1285,
            Victories = 14,
            Defeats = 4
        };
        db.Players.Add(camille);

        // 2. Catalogue d'espèces réelles (GBIF / iNaturalist)
        var spMartin = new Species
        {
            Id = Guid.Parse("a1111111-1111-1111-1111-111111111101"),
            ScientificName = "Alcedo atthis",
            CommonName = "Martin-pêcheur d'Europe",
            Family = "Alcedinidae",
            Kingdom = "Animalia",
            Habitat = "Zone humide",
            Description = "Petit oiseau piscivore au plumage bleu-vert iridescent, plongeant à grande vitesse depuis un perchoir riverain.",
            ImageUrl = "https://images.unsplash.com/photo-1544923408-75c5cef46f14?auto=format&fit=crop&w=700&q=80",
            MaxWeightKg = 0.042,
            MaxSpeedKmh = 45.0,
            DangerIndex = 2.5,
            GbifOccurrences = 6500,
            BaseHp = 78,
            BaseAttack = 82,
            BaseDefense = 54,
            BaseSpeed = 88,
            Rarity = CardRarity.Rare
        };

        var spRenard = new Species
        {
            Id = Guid.Parse("a1111111-1111-1111-1111-111111111102"),
            ScientificName = "Vulpes vulpes",
            CommonName = "Renard roux",
            Family = "Canidae",
            Kingdom = "Animalia",
            Habitat = "Forêt",
            Description = "Carnivore opportuniste extrêmement adaptable, doté d'une ouïe fine capable de localiser des rongeurs sous la neige.",
            ImageUrl = "https://images.unsplash.com/photo-1474511320723-9a56873867b5?auto=format&fit=crop&w=700&q=80",
            MaxWeightKg = 7.5,
            MaxSpeedKmh = 50.0,
            DangerIndex = 5.5,
            GbifOccurrences = 24000,
            BaseHp = 135,
            BaseAttack = 128,
            BaseDefense = 92,
            BaseSpeed = 76,
            Rarity = CardRarity.PeuCommune
        };

        var spLynx = new Species
        {
            Id = Guid.Parse("a1111111-1111-1111-1111-111111111103"),
            ScientificName = "Lynx lynx",
            CommonName = "Lynx roux",
            Family = "Felidae",
            Kingdom = "Animalia",
            Habitat = "Forêt",
            Description = "Félin boréal discret reconnaissable à ses pinceaux noirs au bout des oreilles et ses larges pattes adaptées à la neige.",
            ImageUrl = "https://images.unsplash.com/photo-1561731216-c3a4d99437d5?auto=format&fit=crop&w=700&q=80",
            MaxWeightKg = 22.0,
            MaxSpeedKmh = 65.0,
            DangerIndex = 7.8,
            GbifOccurrences = 14500,
            BaseHp = 165,
            BaseAttack = 170,
            BaseDefense = 115,
            BaseSpeed = 84,
            Rarity = CardRarity.Commune
        };

        var spFougere = new Species
        {
            Id = Guid.Parse("a1111111-1111-1111-1111-111111111104"),
            ScientificName = "Dryopteris filix-mas",
            CommonName = "Fougère mâle",
            Family = "Dryopteridaceae",
            Kingdom = "Plantae",
            Habitat = "Forêt",
            Description = "Plante vasculaire cryptogame des sous-bois humides et ombragés, formant de larges frondes en entonnoir.",
            ImageUrl = "https://images.unsplash.com/photo-1518531933037-91b2f5f229cc?auto=format&fit=crop&w=700&q=80",
            MaxWeightKg = 1.2,
            MaxSpeedKmh = 1.0,
            DangerIndex = 1.0,
            GbifOccurrences = 42000,
            BaseHp = 145,
            BaseAttack = 35,
            BaseDefense = 130,
            BaseSpeed = 10,
            Rarity = CardRarity.PeuCommune
        };

        var spChene = new Species
        {
            Id = Guid.Parse("a1111111-1111-1111-1111-111111111105"),
            ScientificName = "Quercus robur",
            CommonName = "Chêne pédonculé",
            Family = "Fagaceae",
            Kingdom = "Plantae",
            Habitat = "Forêt",
            Description = "Arbre majestueux des forêts tempérées européennes, abritant des centaines d'espèces d'insectes et d'oiseaux.",
            ImageUrl = "https://images.unsplash.com/photo-1542273917363-3b1817f69a2d?auto=format&fit=crop&w=700&q=80",
            MaxWeightKg = 4500.0,
            MaxSpeedKmh = 1.0,
            DangerIndex = 1.2,
            GbifOccurrences = 95000,
            BaseHp = 295,
            BaseAttack = 45,
            BaseDefense = 210,
            BaseSpeed = 10,
            Rarity = CardRarity.Commune
        };

        db.Species.AddRange(spMartin, spRenard, spLynx, spFougere, spChene);

        // 3. Collection des 6 cartes exactes de la page 17 du Cahier des Charges (Figma "Ma collection")
        var cardMartin1 = new Card
        {
            Id = Guid.Parse("c1111111-1111-1111-1111-111111111101"),
            SpecimenNumber = 24,
            OwnerId = camille.Id,
            SpeciesId = spMartin.Id,
            CustomName = "Martin-pêcheur",
            ScientificSubtitle = "Alcedo atthis",
            FamilyName = "Alcedinidae",
            HabitatName = "Zone humide",
            ImageUrl = spMartin.ImageUrl,
            Rarity = CardRarity.Rare,
            Stage = EvolutionStage.Adulte,
            CurrentXp = 640,
            MaxXpForEvolution = 1000,
            Hp = 96,
            Attack = 104,
            Defense = 68,
            Speed = 92,
            WeightKg = 0.042,
            DangerIndex = 2.5
        };

        var cardLynx = new Card
        {
            Id = Guid.Parse("c1111111-1111-1111-1111-111111111102"),
            SpecimenNumber = 15,
            OwnerId = camille.Id,
            SpeciesId = spLynx.Id,
            CustomName = "Lynx roux",
            ScientificSubtitle = "Lynx rufus",
            FamilyName = "Felidae",
            HabitatName = "Forêt",
            ImageUrl = spLynx.ImageUrl,
            Rarity = CardRarity.Commune,
            Stage = EvolutionStage.Adulte,
            CurrentXp = 820,
            MaxXpForEvolution = 1000,
            Hp = 165,
            Attack = 170,
            Defense = 115,
            Speed = 84,
            WeightKg = 22.0,
            DangerIndex = 7.8
        };

        var cardRenard = new Card
        {
            Id = Guid.Parse("c1111111-1111-1111-1111-111111111103"),
            SpecimenNumber = 19,
            OwnerId = camille.Id,
            SpeciesId = spRenard.Id,
            CustomName = "Renard roux",
            ScientificSubtitle = "Vulpes vulpes",
            FamilyName = "Canidae",
            HabitatName = "Forêt",
            ImageUrl = spRenard.ImageUrl,
            Rarity = CardRarity.PeuCommune,
            Stage = EvolutionStage.Adulte,
            CurrentXp = 540,
            MaxXpForEvolution = 1000,
            Hp = 135,
            Attack = 128,
            Defense = 92,
            Speed = 76,
            WeightKg = 7.5,
            DangerIndex = 5.5
        };

        var cardHybride = new Card
        {
            Id = Guid.Parse("c1111111-1111-1111-1111-111111111104"),
            SpecimenNumber = 31,
            OwnerId = camille.Id,
            IsHybrid = true,
            ParentACardId = cardRenard.Id,
            ParentBCardId = cardLynx.Id,
            CustomName = "Renard × Lynx",
            ScientificSubtitle = "Croisement fictif · Rare",
            FamilyName = "Croisement fictif",
            HabitatName = "Forêt",
            ImageUrl = "https://images.unsplash.com/photo-1516934024742-b461fba47600?auto=format&fit=crop&w=700&q=80",
            Rarity = CardRarity.Rare,
            Stage = EvolutionStage.Adulte,
            CurrentXp = 780,
            MaxXpForEvolution = 1000,
            Hp = 188,
            Attack = 192,
            Defense = 132,
            Speed = 89,
            WeightKg = 14.8,
            DangerIndex = 7.2
        };

        var cardFougere = new Card
        {
            Id = Guid.Parse("c1111111-1111-1111-1111-111111111105"),
            SpecimenNumber = 8,
            OwnerId = camille.Id,
            SpeciesId = spFougere.Id,
            CustomName = "Fougère mâle",
            ScientificSubtitle = "Dryopteris filix-mas",
            FamilyName = "Dryopteridaceae",
            HabitatName = "Forêt",
            ImageUrl = spFougere.ImageUrl,
            Rarity = CardRarity.PeuCommune,
            Stage = EvolutionStage.Juvenile,
            CurrentXp = 320,
            MaxXpForEvolution = 1000,
            Hp = 145,
            Attack = 35,
            Defense = 130,
            Speed = 10,
            WeightKg = 1.2,
            DangerIndex = 1.0
        };

        var cardMartin2 = new Card
        {
            Id = Guid.Parse("c1111111-1111-1111-1111-111111111106"),
            SpecimenNumber = 12,
            OwnerId = camille.Id,
            SpeciesId = spMartin.Id,
            CustomName = "Martin-pêcheur",
            ScientificSubtitle = "Alcedo atthis",
            FamilyName = "Alcedinidae",
            HabitatName = "Zone humide",
            ImageUrl = spMartin.ImageUrl,
            Rarity = CardRarity.Rare,
            Stage = EvolutionStage.Juvenile,
            CurrentXp = 410,
            MaxXpForEvolution = 1000,
            Hp = 78,
            Attack = 82,
            Defense = 54,
            Speed = 88,
            WeightKg = 0.042,
            DangerIndex = 2.5
        };

        // 4. Vitrines 3D / Biomes (Figma Page 16 : "La Clairière", "Les Dunes", "Les Berges")
        var biomeClairiere = new BiomeShowcase
        {
            Id = Guid.Parse("b1111111-1111-1111-1111-111111111101"),
            OwnerId = camille.Id,
            Name = "La Clairière",
            BiomeType = "Forêt",
            Subtitle = "Sous-bois, mousse & lumière tamisée",
            BackgroundImageUrl = "https://images.unsplash.com/photo-1448375240586-882707db888b?auto=format&fit=crop&w=900&q=80",
            IsActive = true,
            MaxSlots = 8,
            ExposedCardIds = new List<Guid> { cardMartin1.Id, cardFougere.Id }
        };
        cardMartin1.DisplayedInBiomeId = biomeClairiere.Id;
        cardFougere.DisplayedInBiomeId = biomeClairiere.Id;

        var biomeDunes = new BiomeShowcase
        {
            Id = Guid.Parse("b1111111-1111-1111-1111-111111111102"),
            OwnerId = camille.Id,
            Name = "Les Dunes",
            BiomeType = "Désert",
            Subtitle = "Grès ocre, vents secs & canyons chauds",
            BackgroundImageUrl = "https://images.unsplash.com/photo-1509316975850-ff9c5deb0cd9?auto=format&fit=crop&w=900&q=80",
            IsActive = false,
            MaxSlots = 8,
            ExposedCardIds = new List<Guid> { cardLynx.Id }
        };
        cardLynx.DisplayedInBiomeId = biomeDunes.Id;

        var biomeBerges = new BiomeShowcase
        {
            Id = Guid.Parse("b1111111-1111-1111-1111-111111111103"),
            OwnerId = camille.Id,
            Name = "Les Berges",
            BiomeType = "Zone humide",
            Subtitle = "Berge végétalisée, roseaux & eau douce",
            BackgroundImageUrl = "https://images.unsplash.com/photo-1470071459604-3b5ec3a7fe05?auto=format&fit=crop&w=900&q=80",
            IsActive = false,
            MaxSlots = 8,
            ExposedCardIds = new List<Guid> { cardMartin2.Id }
        };
        cardMartin2.DisplayedInBiomeId = biomeBerges.Id;

        db.Cards.AddRange(cardMartin1, cardHybride, cardLynx, cardRenard, cardFougere, cardMartin2);
        db.Biomes.AddRange(biomeClairiere, biomeDunes, biomeBerges);

        // 5. Deck de combat actif
        db.Decks.Add(new Deck
        {
            PlayerId = camille.Id,
            Name = "Escouade Forestière",
            IsActive = true,
            ActiveCardIds = new List<Guid> { cardHybride.Id, cardLynx.Id, cardMartin1.Id }
        });

        // 6. Zones d'expédition DBSCAN (Globe 3D - Figma Page 18)
        db.HotspotZones.AddRange(
            new GeoHotspotZone
            {
                RegionName = "Vallée de la Loire",
                TargetScientificName = "Alcedo atthis",
                TargetCommonName = "Martin-pêcheur d'Europe",
                Latitude = 47.3900,
                Longitude = 0.6889,
                RadiusKm = 95.0,
                OptimalSeason = "Printemps · Été",
                IsSeasonOpen = true,
                DensityScore = 0.92
            },
            new GeoHotspotZone
            {
                RegionName = "Réserve de Camargue",
                TargetScientificName = "Alcedo atthis",
                TargetCommonName = "Martin-pêcheur d'Europe",
                Latitude = 43.5283,
                Longitude = 4.6278,
                RadiusKm = 70.0,
                OptimalSeason = "Automne",
                IsSeasonOpen = true,
                DensityScore = 0.85
            },
            new GeoHotspotZone
            {
                RegionName = "Bassin Alpin · Lac d'Annecy",
                TargetScientificName = "Vulpes vulpes",
                TargetCommonName = "Renard roux",
                Latitude = 45.8992,
                Longitude = 6.1294,
                RadiusKm = 65.0,
                OptimalSeason = "Automne · Hiver",
                IsSeasonOpen = true,
                DensityScore = 0.95
            },
            new GeoHotspotZone
            {
                RegionName = "Massif du Jura",
                TargetScientificName = "Lynx lynx",
                TargetCommonName = "Lynx roux",
                Latitude = 46.5510,
                Longitude = 5.9120,
                RadiusKm = 80.0,
                OptimalSeason = "Hiver · Printemps",
                IsSeasonOpen = true,
                DensityScore = 0.88
            }
        );

        // 7. Questions du Quiz Chronométré (Figma Page 17 : "Quelle photographie représente une plante ?")
        db.QuizQuestions.AddRange(
            new QuizQuestion
            {
                Id = Guid.Parse("d1111111-1111-1111-1111-111111111101"),
                Theme = "Reconnaître les espèces",
                ScientificSubtitle = "Alcedo atthis",
                ImageUrl = spMartin.ImageUrl,
                QuestionText = "Quelle photographie ou espèce parmi ces propositions représente une plante ?",
                Options = new List<string> { "Le martin-pêcheur", "La fougère mâle", "Le lynx roux" },
                CorrectOptionIndex = 1,
                TimeLimitSeconds = 10,
                RewardXp = 85,
                RewardFeuilles = 50
            },
            new QuizQuestion
            {
                Id = Guid.Parse("d1111111-1111-1111-1111-111111111102"),
                Theme = "Biologie & Habitat",
                ScientificSubtitle = "Alcedo atthis",
                ImageUrl = spMartin.ImageUrl,
                QuestionText = "Dans quel biome principal le Martin-pêcheur d'Europe (Alcedo atthis) chasse-t-il ses proies ?",
                Options = new List<string> { "Zone humide et berges d'eau douce", "Dunes arides du désert", "Toundra polaire glacée" },
                CorrectOptionIndex = 0,
                TimeLimitSeconds = 10,
                RewardXp = 90,
                RewardFeuilles = 60
            },
            new QuizQuestion
            {
                Id = Guid.Parse("d1111111-1111-1111-1111-111111111103"),
                Theme = "Taxonomie Scientifique",
                ScientificSubtitle = "Vulpes vulpes",
                ImageUrl = spRenard.ImageUrl,
                QuestionText = "À quelle famille taxonomique appartient le Renard roux (Vulpes vulpes) ?",
                Options = new List<string> { "Felidae", "Canidae", "Alcedinidae" },
                CorrectOptionIndex = 1,
                TimeLimitSeconds = 10,
                RewardXp = 80,
                RewardFeuilles = 50
            }
        );

        // 8. Les 6 Enchères exactes de la page 19 du Cahier des Charges (Figma "Marché aux enchères")
        var now = DateTimeOffset.UtcNow;
        var sellerCards = new[]
        {
            new Card { OwnerId = Guid.NewGuid(), CustomName = "Chêne pédonculé", ScientificSubtitle = "Quercus robur", FamilyName = "Plante · Commune", HabitatName = "Forêt", ImageUrl = spChene.ImageUrl, Rarity = CardRarity.Commune, Stage = EvolutionStage.Adulte, Hp = 295, Attack = 45, Defense = 210, Speed = 10 },
            new Card { OwnerId = Guid.NewGuid(), CustomName = "Fougère mâle", ScientificSubtitle = "Dryopteris filix-mas", FamilyName = "Plante · Peu commune", HabitatName = "Forêt", ImageUrl = spFougere.ImageUrl, Rarity = CardRarity.PeuCommune, Stage = EvolutionStage.Adulte, Hp = 145, Attack = 35, Defense = 130, Speed = 10 },
            new Card { OwnerId = Guid.NewGuid(), CustomName = "Renard roux", ScientificSubtitle = "Vulpes vulpes", FamilyName = "Animal · Peu commune", HabitatName = "Forêt", ImageUrl = spRenard.ImageUrl, Rarity = CardRarity.PeuCommune, Stage = EvolutionStage.Adulte, Hp = 135, Attack = 128, Defense = 92, Speed = 76 },
            new Card { OwnerId = Guid.NewGuid(), CustomName = "Martin-pêcheur", ScientificSubtitle = "Alcedo atthis", FamilyName = "Animal · Rare", HabitatName = "Zone humide", ImageUrl = spMartin.ImageUrl, Rarity = CardRarity.Rare, Stage = EvolutionStage.Adulte, Hp = 96, Attack = 104, Defense = 68, Speed = 92 },
            new Card { OwnerId = Guid.NewGuid(), CustomName = "Lynx roux", ScientificSubtitle = "Lynx rufus", FamilyName = "Animal · Commune", HabitatName = "Forêt", ImageUrl = spLynx.ImageUrl, Rarity = CardRarity.Commune, Stage = EvolutionStage.Adulte, Hp = 165, Attack = 170, Defense = 115, Speed = 84 },
            new Card { OwnerId = Guid.NewGuid(), CustomName = "Renard × Lynx", ScientificSubtitle = "Croisement fictif · Hybride", FamilyName = "Hybride · Singulière", HabitatName = "Forêt", ImageUrl = cardHybride.ImageUrl, Rarity = CardRarity.Rare, IsHybrid = true, Stage = EvolutionStage.Adulte, Hp = 188, Attack = 192, Defense = 132, Speed = 89 }
        };
        db.Cards.AddRange(sellerCards);

        db.AuctionListings.AddRange(
            new AuctionListing
            {
                LotNumber = 204,
                CardId = sellerCards[0].Id,
                Card = sellerCards[0],
                SellerId = sellerCards[0].OwnerId,
                SellerPseudo = "Léon",
                StartingPrice = 100,
                CurrentBid = 120,
                BidCount = 2,
                EndsAt = now.AddMinutes(22)
            },
            new AuctionListing
            {
                LotNumber = 205,
                CardId = sellerCards[1].Id,
                Card = sellerCards[1],
                SellerId = sellerCards[1].OwnerId,
                SellerPseudo = "Iris",
                StartingPrice = 70,
                CurrentBid = 80,
                BidCount = 1,
                EndsAt = now.AddMinutes(47)
            },
            new AuctionListing
            {
                LotNumber = 201,
                CardId = sellerCards[2].Id,
                Card = sellerCards[2],
                SellerId = sellerCards[2].OwnerId,
                SellerPseudo = "Léo",
                StartingPrice = 120,
                CurrentBid = 160,
                BidCount = 3,
                EndsAt = now.AddMinutes(92)
            },
            new AuctionListing
            {
                LotNumber = 202,
                CardId = sellerCards[3].Id,
                Card = sellerCards[3],
                SellerId = sellerCards[3].OwnerId,
                SellerPseudo = "Noa",
                StartingPrice = 250,
                CurrentBid = 340,
                BidCount = 5,
                EndsAt = now.AddHours(3).AddMinutes(10)
            },
            new AuctionListing
            {
                LotNumber = 206,
                CardId = sellerCards[4].Id,
                Card = sellerCards[4],
                SellerId = sellerCards[4].OwnerId,
                SellerPseudo = "Hugo",
                StartingPrice = 110,
                CurrentBid = 140,
                BidCount = 2,
                EndsAt = now.AddHours(5).AddMinutes(40)
            },
            new AuctionListing
            {
                LotNumber = 203,
                CardId = sellerCards[5].Id,
                Card = sellerCards[5],
                SellerId = sellerCards[5].OwnerId,
                SellerPseudo = "Sora",
                StartingPrice = 380,
                CurrentBid = 480,
                BidCount = 7,
                EndsAt = now.AddHours(6).AddMinutes(5)
            }
        );

        await db.SaveChangesAsync();
    }
}
