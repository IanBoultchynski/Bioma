using System.Diagnostics;
using Bioma.Application.DTOs;
using Bioma.Application.Interfaces;
using Bioma.Domain.Entities;
using Bioma.Domain.Enums;
using Bioma.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bioma.Infrastructure.Services;

/// <summary>
/// Implémentation complète de l'Épique 11 : Vitrine 3D / Terrarium Virtuel (US-61 à US-63).
/// - US-61 : Accès au terrarium / musée virtuel 3D avec détection dynamique des fichiers Blender (.blend) et GLB (.glb) dans wwwroot/models/biomes/
/// - US-62 : Exposition et positionnement des cartes du joueur sur des socles 3D directement dans le biome
/// - Filtrage écologique strict : seuls les animaux associés au biome actif peuvent être exposés sur les socles de ce biome
/// </summary>
public class Showcase3DService : IShowcase3DService
{
    public const int PedestalsPerBiome = 5;
    private const int BaseSlotOffset = 10_000;
    private const int BiomeSlotMultiplier = 100;

    public record BiomeDefinition(
        BiomeType Biome,
        string Label,
        string CanonicalBlendFile,
        string CanonicalGlbFile,
        string[] FileAliases,
        string Description,
        string SampleSpecies
    );

    public static readonly IReadOnlyList<BiomeDefinition> AllBiomeDefinitions =
    [
        new(
            BiomeType.Taiga,
            "Forêt de Sapins",
            "BiomeSapins.blend",
            "BiomeSapins.glb",
            ["BiomeSapins", "Sapins", "BiomeTaiga", "Taiga"],
            "Massif boréal de sapins et rochers moussus.",
            ""
        ),
        new(
            BiomeType.Toundra,
            "Arctique",
            "artique.blend",
            "artique.glb",
            ["artique", "arctique", "BiomeArctique", "Toundra"],
            "Banquise polaire, glace et étendues enneigées.",
            ""
        ),
        new(
            BiomeType.Desert,
            "Désert",
            "desert.blend",
            "desert.glb",
            ["desert", "BiomeDesert"],
            "Dunes dorées, relief aride et cactus du désert.",
            ""
        ),
        new(
            BiomeType.Montagne,
            "Montagne",
            "Montain.blend",
            "Montain.glb",
            ["Montain", "Montagne", "BiomeMontagne"],
            "Sommets rocheux escarpés et cimes enneigées.",
            ""
        ),
        new(
            BiomeType.ForetTemperee,
            "Plaine",
            "plain.blend",
            "plain.glb",
            ["plain", "plaine", "BiomePlaine", "ForetTemperee"],
            "Prairie verdoyante et collines fleuries.",
            ""
        ),
        new(
            BiomeType.Savane,
            "Savane",
            "savane.blend",
            "savane.glb",
            ["savane", "BiomeSavane"],
            "Plaines dorées ponctuées d'acacias sous le soleil.",
            ""
        ),
        new(
            BiomeType.ForetTropicale,
            "Jungle",
            "jungle.blend",
            "jungle.glb",
            ["jungle", "BiomeJungle"],
            "Canopée tropicale dense et végétation luxuriante.",
            ""
        ),
        new(
            BiomeType.ZoneUrbaine,
            "Ville",
            "ville.blend",
            "ville.glb",
            ["ville", "BiomeVille"],
            "Paysage urbain et espaces citadins aménagés.",
            ""
        ),
        new(
            BiomeType.MilieuMarin,
            "Océan",
            "ocean.blend",
            "ocean.glb",
            ["ocean", "océan", "BiomeOcean"],
            "Eaux profondes et récifs marins.",
            ""
        ),
        new(
            BiomeType.ZoneHumide,
            "Marais",
            "marais.blend",
            "marais.glb",
            ["marais", "BiomeMarais"],
            "Eaux calmes, roseaux et terres marécageuses.",
            ""
        )
    ];

    /// <summary>
    /// Coordonnées 3D par défaut des 5 socles d'exposition (clairières dégagées entre les sapins et rochers sur le plateau 6x6).
    /// </summary>
    public static readonly (string Label, double X, double Y, double Z)[] DefaultPedestalClearings =
    [
        ("Socle #1 · Clairière Centrale",   -0.20, 0.30,  0.60),
        ("Socle #2 · Clairière Est",         1.40, 0.30, -0.40),
        ("Socle #3 · Clairière Sud-Ouest",  -0.80, 0.30,  1.60),
        ("Socle #4 · Lisière Sud",           0.20, 0.30,  2.20),
        ("Socle #5 · Clairière Nord-Ouest", -2.20, 0.30, -0.80)
    ];

    private static readonly Dictionary<string, BiomeType> SpeciesBiomeCatalog = new(StringComparer.OrdinalIgnoreCase)
    {
        // BiomeSapins / Taïga & Forêt de Conifères (BiomeType.Taiga)
        ["Vulpes vulpes"]            = BiomeType.Taiga,
        ["Haliaeetus leucocephalus"] = BiomeType.Taiga,
        ["Sciurus vulgaris"]         = BiomeType.Taiga,
        ["Lynx lynx"]                = BiomeType.Taiga,
        ["Canis lupus"]              = BiomeType.Taiga,
        ["Ursus arctos"]             = BiomeType.Taiga,
        ["Gulo gulo"]                = BiomeType.Taiga,
        ["Bubo bubo"]                = BiomeType.Taiga,
        ["Capreolus capreolus"]      = BiomeType.Taiga,
        ["Alces alces"]              = BiomeType.Taiga,
        ["Tetrao urogallus"]         = BiomeType.Taiga,
        ["Cervus elaphus"]           = BiomeType.Taiga,
        ["Martes martes"]            = BiomeType.Taiga,
        ["Strix nebulosa"]           = BiomeType.Taiga,
        ["Loxia curvirostra"]        = BiomeType.Taiga,

        // Forêt Tempérée Feuillue (BiomeType.ForetTemperee)
        ["Erinaceus europaeus"]      = BiomeType.ForetTemperee,
        ["Erithacus rubecula"]       = BiomeType.ForetTemperee,
        ["Meles meles"]              = BiomeType.ForetTemperee,
        ["Salamandra salamandra"]    = BiomeType.ForetTemperee,
        ["Lynx pardinus"]            = BiomeType.ForetTemperee,
        ["Sus scrofa"]               = BiomeType.ForetTemperee,
        ["Dama dama"]                = BiomeType.ForetTemperee,

        // Savane Africaine (BiomeType.Savane)
        ["Panthera leo"]             = BiomeType.Savane,
        ["Acinonyx jubatus"]         = BiomeType.Savane,
        ["Loxodonta africana"]       = BiomeType.Savane,
        ["Giraffa camelopardalis"]   = BiomeType.Savane,
        ["Equus quagga"]             = BiomeType.Savane,
        ["Crocuta crocuta"]          = BiomeType.Savane,
        ["Lycaon pictus"]            = BiomeType.Savane,
        ["Struthio camelus"]         = BiomeType.Savane,

        // Forêt Tropicale & Jungle (BiomeType.ForetTropicale)
        ["Pongo abelii"]             = BiomeType.ForetTropicale,
        ["Pongo pygmaeus"]           = BiomeType.ForetTropicale,
        ["Ailuropoda melanoleuca"]   = BiomeType.ForetTropicale,
        ["Panthera onca"]            = BiomeType.ForetTropicale,
        ["Panthera tigris"]          = BiomeType.ForetTropicale,
        ["Ara macao"]                = BiomeType.ForetTropicale,
        ["Gorilla beringei"]         = BiomeType.ForetTropicale,
        ["Pan troglodytes"]          = BiomeType.ForetTropicale,

        // Haute Montagne (BiomeType.Montagne)
        ["Panthera uncia"]           = BiomeType.Montagne,
        ["Rupicapra rupicapra"]      = BiomeType.Montagne,
        ["Capra ibex"]               = BiomeType.Montagne,
        ["Aquila chrysaetos"]        = BiomeType.Montagne,
        ["Marmota marmota"]          = BiomeType.Montagne,
        ["Gypaetus barbatus"]        = BiomeType.Montagne,

        // Toundra & Arctique (BiomeType.Toundra)
        ["Ursus maritimus"]          = BiomeType.Toundra,
        ["Vulpes lagopus"]           = BiomeType.Toundra,
        ["Rangifer tarandus"]        = BiomeType.Toundra,
        ["Bubo scandiacus"]          = BiomeType.Toundra,
        ["Ovibos moschatus"]         = BiomeType.Toundra,
        ["Odobenus rosmarus"]        = BiomeType.Toundra,

        // Eau Douce & Rivières (BiomeType.EauDouce)
        ["Castor fiber"]             = BiomeType.EauDouce,
        ["Castor canadensis"]        = BiomeType.EauDouce,
        ["Lutra lutra"]              = BiomeType.EauDouce,
        ["Alcedo atthis"]            = BiomeType.EauDouce,
        ["Anas platyrhynchos"]       = BiomeType.EauDouce,
        ["Ornithorhynchus anatinus"] = BiomeType.EauDouce,
        ["Cygnus olor"]              = BiomeType.EauDouce,

        // Désert Aride (BiomeType.Desert)
        ["Vulpes zerda"]             = BiomeType.Desert,
        ["Camelus dromedarius"]      = BiomeType.Desert,
        ["Camelus bactrianus"]       = BiomeType.Desert,
        ["Oryx gazella"]             = BiomeType.Desert,
        ["Addax nasomaculatus"]      = BiomeType.Desert,
        ["Suricata suricatta"]       = BiomeType.Desert,

        // Milieu Marin (BiomeType.MilieuMarin)
        ["Delphinus delphis"]        = BiomeType.MilieuMarin,
        ["Tursiops truncatus"]       = BiomeType.MilieuMarin,
        ["Orcinus orca"]             = BiomeType.MilieuMarin,
        ["Megaptera novaeangliae"]   = BiomeType.MilieuMarin,
        ["Chelonia mydas"]           = BiomeType.MilieuMarin,
        ["Carcharodon carcharias"]   = BiomeType.MilieuMarin,

        // Zone Humide (BiomeType.ZoneHumide)
        ["Phoenicopterus roseus"]    = BiomeType.ZoneHumide,
        ["Ardea cinerea"]            = BiomeType.ZoneHumide,
        ["Alligator mississippiensis"] = BiomeType.ZoneHumide,
        ["Crocodylus niloticus"]     = BiomeType.ZoneHumide,
        ["Hydrochoerus hydrochaeris"] = BiomeType.ZoneHumide,

        // Zone Urbaine (BiomeType.ZoneUrbaine)
        ["Columba livia"]            = BiomeType.ZoneUrbaine,
        ["Passer domesticus"]        = BiomeType.ZoneUrbaine,
        ["Falco peregrinus"]         = BiomeType.ZoneUrbaine,
        ["Procyon lotor"]            = BiomeType.ZoneUrbaine
    };

    private readonly BiomaDbContext _db;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public Showcase3DService(BiomaDbContext db)
    {
        _db = db;
    }

    public static string GetBiomeLabel(BiomeType biome) =>
        AllBiomeDefinitions.FirstOrDefault(b => b.Biome == biome)?.Label ?? biome.ToString();

    public static BiomeType ParseOrResolveBiome(string? biomeString, string? scientificName, string? commonName = null)
    {
        if (!string.IsNullOrWhiteSpace(scientificName) &&
            SpeciesBiomeCatalog.TryGetValue(scientificName.Trim(), out var mappedBiome))
        {
            return mappedBiome;
        }

        if (!string.IsNullOrWhiteSpace(biomeString) &&
            Enum.TryParse<BiomeType>(biomeString, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return ResolveBiomeForSpecies(scientificName ?? string.Empty, commonName);
    }

    /// <summary>
    /// Détermine scientifiquement le biome d'une espèce à partir de son nom binomial, de son nom commun ou de sa latitude.
    /// </summary>
    public static BiomeType ResolveBiomeForSpecies(string scientificName, string? commonName = null, double? latitude = null)
    {
        var cleanSci = (scientificName ?? string.Empty).Trim();
        if (SpeciesBiomeCatalog.TryGetValue(cleanSci, out var exact))
        {
            return exact;
        }

        string combined = $"{cleanSci} {commonName}".ToLowerInvariant();

        if (combined.Contains("sapin") || combined.Contains("boréal") || combined.Contains("boreal") ||
            combined.Contains("lynx") || combined.Contains("lupus") || combined.Contains("loup") ||
            combined.Contains("arctos") || combined.Contains("ours brun") || combined.Contains("gulo") ||
            combined.Contains("sciurus") || combined.Contains("écureuil") || combined.Contains("bubo") ||
            combined.Contains("haliaeetus") || combined.Contains("pygargue") || combined.Contains("vulpes vulpes") ||
            combined.Contains("capreolus") || combined.Contains("chevreuil") || combined.Contains("cervus") ||
            combined.Contains("alces"))
        {
            return BiomeType.Taiga;
        }

        if (combined.Contains("maritimus") || combined.Contains("polaire") || combined.Contains("arctique") ||
            combined.Contains("lagopus") || combined.Contains("rangifer") || combined.Contains("renne") ||
            combined.Contains("scandiacus") || combined.Contains("harfang") || combined.Contains("morse"))
        {
            return BiomeType.Toundra;
        }

        if (combined.Contains("uncia") || combined.Contains("neiges") || combined.Contains("rupicapra") ||
            combined.Contains("chamois") || combined.Contains("ibex") || combined.Contains("bouquetin") ||
            combined.Contains("marmota") || combined.Contains("marmotte") || combined.Contains("alpin"))
        {
            return BiomeType.Montagne;
        }

        if (combined.Contains("leo") || combined.Contains("lion") || combined.Contains("savane") ||
            combined.Contains("jubatus") || combined.Contains("guépard") || combined.Contains("loxodonta") ||
            combined.Contains("éléphant") || combined.Contains("giraffa") || combined.Contains("girafe") ||
            combined.Contains("quagga") || combined.Contains("zèbre") || combined.Contains("crocuta"))
        {
            return BiomeType.Savane;
        }

        if (combined.Contains("pongo") || combined.Contains("orang") || combined.Contains("melanoleuca") ||
            combined.Contains("panda") || combined.Contains("onca") || combined.Contains("jaguar") ||
            combined.Contains("tigris") || combined.Contains("tigre") || combined.Contains("ara ") ||
            combined.Contains("gorilla") || combined.Contains("tropical"))
        {
            return BiomeType.ForetTropicale;
        }

        if (combined.Contains("castor") || combined.Contains("lutra") || combined.Contains("loutre") ||
            combined.Contains("alcedo") || combined.Contains("martin-pêcheur") || combined.Contains("platyrhynchos") ||
            combined.Contains("colvert") || combined.Contains("canard") || combined.Contains("cygnus"))
        {
            return BiomeType.EauDouce;
        }

        if (combined.Contains("zerda") || combined.Contains("fennec") || combined.Contains("camelus") ||
            combined.Contains("dromadaire") || combined.Contains("chameau") || combined.Contains("oryx") ||
            combined.Contains("désert") || combined.Contains("suricata"))
        {
            return BiomeType.Desert;
        }

        if (combined.Contains("delphinus") || combined.Contains("tursiops") || combined.Contains("dauphin") ||
            combined.Contains("orcinus") || combined.Contains("orque") || combined.Contains("baleine") ||
            combined.Contains("chelonia") || combined.Contains("tortue marine") || combined.Contains("carcharodon") ||
            combined.Contains("requin"))
        {
            return BiomeType.MilieuMarin;
        }

        if (combined.Contains("phoenicopterus") || combined.Contains("flamant") || combined.Contains("ardea") ||
            combined.Contains("héron") || combined.Contains("alligator") || combined.Contains("crocodylus") ||
            combined.Contains("capybara"))
        {
            return BiomeType.ZoneHumide;
        }

        if (latitude.HasValue)
        {
            double absLat = Math.Abs(latitude.Value);
            if (absLat >= 66.0) return BiomeType.Toundra;
            if (absLat >= 52.0) return BiomeType.Taiga;
            if (absLat <= 15.0) return BiomeType.ForetTropicale;
            if (absLat <= 28.0) return BiomeType.Savane;
        }

        return BiomeType.ForetTemperee;
    }

    public static string ResolveBiomesDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "wwwroot", "models", "biomes"),
            @"w:\bioma\frontend-blazor\Bioma.Client\wwwroot\models\biomes"
        };

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Bioma.sln")))
        {
            dir = dir.Parent;
        }

        if (dir is not null)
        {
            var slnCandidate = Path.Combine(dir.FullName, "frontend-blazor", "Bioma.Client", "wwwroot", "models", "biomes");
            Directory.CreateDirectory(slnCandidate);
            return slnCandidate;
        }

        foreach (var c in candidates)
        {
            if (Directory.Exists(c))
            {
                return c;
            }
        }

        var fallback = candidates[^1];
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    /// <summary>
    /// Analyse le dossier wwwroot/models/biomes/ :
    /// - Détecte tous les fichiers .blend et .glb déposés par l'utilisateur
    /// - Si un fichier .blend est présent sans son .glb (ou plus récent que le .glb), tente de le convertir automatiquement via Blender CLI
    /// </summary>
    public static Dictionary<BiomeType, (string? BlendFile, string? GlbUrl, string? PreviewUrl, bool Available)> ScanAvailableBiomeModels()
    {
        var biomesDir = ResolveBiomesDirectory();
        var files = Directory.Exists(biomesDir)
            ? Directory.GetFiles(biomesDir)
            : Array.Empty<string>();

        var result = new Dictionary<BiomeType, (string? BlendFile, string? GlbUrl, string? PreviewUrl, bool Available)>();

        foreach (var def in AllBiomeDefinitions)
        {
            string? matchedBlendPath = null;
            string? matchedGlbPath = null;
            string? matchedPreviewPath = null;

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                var nameWithoutExt = Path.GetFileNameWithoutExtension(file);

                bool matchesAlias = def.FileAliases.Any(alias =>
                    string.Equals(nameWithoutExt, alias, StringComparison.OrdinalIgnoreCase) ||
                    nameWithoutExt.Replace("_", "").Replace("-", "").Equals(alias, StringComparison.OrdinalIgnoreCase));

                if (!matchesAlias) continue;

                if (ext == ".blend")
                {
                    matchedBlendPath = file;
                }
                else if (ext is ".glb" or ".gltf")
                {
                    matchedGlbPath = file;
                }
                else if (ext is ".png" or ".jpg" or ".jpeg" or ".webp")
                {
                    matchedPreviewPath = file;
                }
            }

            // Si un .blend est présent mais que le .glb n'existe pas encore (ou que le .blend a été modifié), conversion automatique via Blender
            if (matchedBlendPath is not null)
            {
                string targetGlbPath = matchedGlbPath ?? Path.Combine(biomesDir, Path.GetFileNameWithoutExtension(matchedBlendPath) + ".glb");
                string targetPngPath = matchedPreviewPath ?? Path.Combine(biomesDir, Path.GetFileNameWithoutExtension(matchedBlendPath) + ".png");

                bool needsGlb = !File.Exists(targetGlbPath) ||
                                File.GetLastWriteTimeUtc(matchedBlendPath) > File.GetLastWriteTimeUtc(targetGlbPath);
                bool needsPng = !File.Exists(targetPngPath);

                if (needsGlb || needsPng)
                {
                    if (TryAutoConvertBlendToGlb(matchedBlendPath, targetGlbPath, needsPng ? targetPngPath : null))
                    {
                        if (File.Exists(targetGlbPath)) matchedGlbPath = targetGlbPath;
                        if (File.Exists(targetPngPath)) matchedPreviewPath = targetPngPath;
                    }
                }
                else
                {
                    if (File.Exists(targetGlbPath)) matchedGlbPath = targetGlbPath;
                    if (File.Exists(targetPngPath)) matchedPreviewPath = targetPngPath;
                }
            }

            string? blendFileName = matchedBlendPath is not null ? Path.GetFileName(matchedBlendPath) : null;
            string? glbUrl = matchedGlbPath is not null && File.Exists(matchedGlbPath)
                ? $"/models/biomes/{Path.GetFileName(matchedGlbPath)}"
                : null;
            string? previewUrl = matchedPreviewPath is not null && File.Exists(matchedPreviewPath)
                ? $"/models/biomes/{Path.GetFileName(matchedPreviewPath)}"
                : null;

            bool isAvailable = glbUrl is not null || blendFileName is not null;
            result[def.Biome] = (blendFileName, glbUrl, previewUrl, isAvailable);
        }

        return result;
    }

    private static bool TryAutoConvertBlendToGlb(string blendFilePath, string targetGlbPath, string? targetPngPath = null)
    {
        try
        {
            var blenderCandidates = new List<string> { "blender" };
            var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var blenderRoot = Path.Combine(progFiles, "Blender Foundation");
            if (Directory.Exists(blenderRoot))
            {
                blenderCandidates.InsertRange(0, Directory.GetFiles(blenderRoot, "blender.exe", SearchOption.AllDirectories)
                    .OrderByDescending(p => p));
            }

            var blenderExe = blenderCandidates.FirstOrDefault(File.Exists);
            if (string.IsNullOrWhiteSpace(blenderExe))
            {
                return false;
            }

            string escapedGlb = targetGlbPath.Replace("\\", "\\\\");
            string pyExpr = $"import bpy; bpy.ops.export_scene.gltf(filepath=r'{escapedGlb}', export_format='GLB', export_apply=True)";

            if (!string.IsNullOrWhiteSpace(targetPngPath))
            {
                string escapedPng = targetPngPath.Replace("\\", "\\\\");
                pyExpr += $"; import mathutils; s=bpy.context.scene; s.render.engine='BLENDER_WORKBENCH'; s.display.shading.light='STUDIO'; s.display.shading.color_type='MATERIAL'; s.render.resolution_x=640; s.render.resolution_y=360; s.render.image_settings.file_format='PNG'; [setattr(m,'diffuse_color',(min(1,pow(n.inputs['Base Color'].default_value[0],0.45)),min(1,pow(n.inputs['Base Color'].default_value[1],0.45)),min(1,pow(n.inputs['Base Color'].default_value[2],0.45)),1)) for m in bpy.data.materials if m.use_nodes for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED']; pts=[o.matrix_world@mathutils.Vector(c) for o in bpy.data.objects if o.type=='MESH' and not o.hide_render for c in o.bound_box]; xs=[p.x for p in pts]; ys=[p.y for p in pts]; zs=[p.z for p in pts]; cx,cy,cz=(min(xs)+max(xs))/2,(min(ys)+max(ys))/2,(min(zs)+max(zs))/2; sp=max(max(xs)-min(xs),max(ys)-min(ys),max(zs)-min(zs)); cd=bpy.data.cameras.new('C'); co=bpy.data.objects.new('C',cd); s.collection.objects.link(co); s.camera=co; d=sp*1.45; co.location=(cx+d*0.75,cy-d*0.75,cz+d*0.62); co.rotation_euler=(mathutils.Vector((cx,cy,cz))-co.location).to_track_quat('-Z','Y').to_euler(); s.render.filepath=r'{escapedPng}'; bpy.ops.render.render(write_still=True)";
            }

            var psi = new ProcessStartInfo
            {
                FileName = blenderExe,
                Arguments = $"-b \"{blendFilePath}\" --python-expr \"{pyExpr}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc is null) return false;
            proc.WaitForExit(15_000);
            return File.Exists(targetGlbPath);
        }
        catch
        {
            return false;
        }
    }

    public static int EncodeGlobalSlotIndex(BiomeType biome, int localSlotIndex) =>
        BaseSlotOffset + ((int)biome * BiomeSlotMultiplier) + Math.Clamp(localSlotIndex, 0, PedestalsPerBiome - 1);

    public async Task<PlayerBiomesOverviewDto> GetPlayerBiomesOverviewAsync(
        Guid playerId,
        CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var player = await LoadPlayerWithCardsAsync(playerId, ct);
            var availableModels = ScanAvailableBiomeModels();
            var allPlayerPedestals = await _db.ShowcasePedestals
                .AsNoTracking()
                .Where(sp => sp.OwnerPlayerId == player.Id && sp.SlotIndex >= BaseSlotOffset)
                .ToListAsync(ct);

            var allInfos = BuildBiomeSlotInfos(availableModels, allPlayerPedestals);

            var created = allInfos.Where(b => b.IsCreatedByPlayer).ToList();
            var availableToCreate = allInfos.Where(b => b.IsAvailable && !b.IsCreatedByPlayer).ToList();

            return new PlayerBiomesOverviewDto(
                player.Id,
                player.Username,
                player.InventoryCards.Count,
                created,
                availableToCreate
            );
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ShowcaseActionResultDto> CreateBiomeAsync(
        Guid playerId,
        BiomeType biome,
        CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var player = await LoadPlayerWithCardsAsync(playerId, ct);
            var availableModels = ScanAvailableBiomeModels();
            if (!availableModels.TryGetValue(biome, out var modelInfo) || !modelInfo.Available)
            {
                return new ShowcaseActionResultDto(
                    false,
                    $"Le biome « {GetBiomeLabel(biome)} » n'est pas encore disponible en 3D.",
                    null);
            }

            int minSlot = EncodeGlobalSlotIndex(biome, 0);
            int maxSlot = EncodeGlobalSlotIndex(biome, PedestalsPerBiome - 1);

            var existing = await _db.ShowcasePedestals
                .Where(sp => sp.OwnerPlayerId == player.Id && sp.SlotIndex >= minSlot && sp.SlotIndex <= maxSlot)
                .ToListAsync(ct);

            if (existing.Count == 0)
            {
                for (int i = 0; i < PedestalsPerBiome; i++)
                {
                    var defPos = DefaultPedestalClearings[i];
                    _db.ShowcasePedestals.Add(new ShowcasePedestal
                    {
                        Id = Guid.NewGuid(),
                        OwnerPlayerId = player.Id,
                        SlotIndex = EncodeGlobalSlotIndex(biome, i),
                        PositionX = defPos.X,
                        PositionY = defPos.Y,
                        PositionZ = defPos.Z,
                        DisplayedPlayerCardId = null
                    });
                }

                player.LastCloudSyncAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
            }

            var view = await GetPlayerShowcaseCoreAsync(player.Id, biome, ct);
            return new ShowcaseActionResultDto(
                true,
                $"Biome « {GetBiomeLabel(biome)} » créé ! Vous pouvez maintenant y exposer vos cartes.",
                view);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ShowcaseActionResultDto> DeleteBiomeAsync(
        Guid playerId,
        BiomeType biome,
        CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            int minSlot = EncodeGlobalSlotIndex(biome, 0);
            int maxSlot = EncodeGlobalSlotIndex(biome, PedestalsPerBiome - 1);

            var existing = await _db.ShowcasePedestals
                .Where(sp => sp.OwnerPlayerId == playerId && sp.SlotIndex >= minSlot && sp.SlotIndex <= maxSlot)
                .ToListAsync(ct);

            if (existing.Count > 0)
            {
                _db.ShowcasePedestals.RemoveRange(existing);
                await _db.SaveChangesAsync(ct);
            }

            return new ShowcaseActionResultDto(
                true,
                $"Biome « {GetBiomeLabel(biome)} » retiré de votre Vitrine 3D.",
                null);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ShowcaseViewDto?> GetPlayerShowcaseAsync(
        Guid playerId,
        BiomeType? selectedBiome = null,
        CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return await GetPlayerShowcaseCoreAsync(playerId, selectedBiome, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ShowcaseActionResultDto> PlaceCardOnPedestalAsync(
        Guid playerId,
        BiomeType biome,
        int localSlotIndex,
        Guid playerCardId,
        CancellationToken ct = default)
    {
        if (localSlotIndex < 0 || localSlotIndex >= PedestalsPerBiome)
        {
            return new ShowcaseActionResultDto(false, $"Emplacement de socle invalide (#{localSlotIndex + 1}).", null);
        }

        await _gate.WaitAsync(ct);
        try
        {
            var availableBiomes = ScanAvailableBiomeModels();
            if (!availableBiomes.TryGetValue(biome, out var biomeInfo) || !biomeInfo.Available)
            {
                var def = AllBiomeDefinitions.First(b => b.Biome == biome);
                return new ShowcaseActionResultDto(
                    false,
                    $"Le modèle 3D du biome « {def.Label} » n'est pas encore présent. Ajoutez « {def.CanonicalBlendFile} » dans wwwroot/models/biomes/.",
                    await GetPlayerShowcaseCoreAsync(playerId, biome, ct));
            }

            var playerCard = await _db.PlayerCards
                .Include(pc => pc.SpeciesCard)
                .FirstOrDefaultAsync(pc => pc.Id == playerCardId && pc.PlayerId == playerId, ct);

            if (playerCard is null)
            {
                return new ShowcaseActionResultDto(
                    false,
                    "Cette carte n'existe pas dans votre inventaire.",
                    await GetPlayerShowcaseCoreAsync(playerId, biome, ct));
            }

            var pedestals = await GetCreatedPedestalsForBiomeAsync(playerId, biome, ct);
            if (pedestals.Count == 0)
            {
                return new ShowcaseActionResultDto(
                    false,
                    $"Vous devez d'abord créer le biome « {GetBiomeLabel(biome)} » dans votre Vitrine 3D.",
                    null);
            }

            // Si cette carte est déjà exposée sur un autre socle de ce biome, on la libère de l'ancien socle
            foreach (var p in pedestals.Where(p => p.DisplayedPlayerCardId == playerCardId))
            {
                p.DisplayedPlayerCardId = null;
            }

            int targetGlobalSlot = EncodeGlobalSlotIndex(biome, localSlotIndex);
            var targetPedestal = pedestals.First(p => p.SlotIndex == targetGlobalSlot);
            targetPedestal.DisplayedPlayerCardId = playerCard.Id;

            var player = await _db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
            if (player is not null)
            {
                player.LastCloudSyncAt = DateTimeOffset.UtcNow;
            }

            await _db.SaveChangesAsync(ct);

            var updatedView = await GetPlayerShowcaseCoreAsync(playerId, biome, ct);
            return new ShowcaseActionResultDto(
                true,
                $"« {playerCard.SpeciesCard?.CommonName} » est désormais exposé sur le Socle 3D #{localSlotIndex + 1} dans {GetBiomeLabel(biome)} !",
                updatedView);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ShowcaseActionResultDto> RemoveCardFromPedestalAsync(
        Guid playerId,
        BiomeType biome,
        int localSlotIndex,
        CancellationToken ct = default)
    {
        if (localSlotIndex < 0 || localSlotIndex >= PedestalsPerBiome)
        {
            return new ShowcaseActionResultDto(false, "Socle 3D introuvable.", null);
        }

        await _gate.WaitAsync(ct);
        try
        {
            var pedestals = await GetCreatedPedestalsForBiomeAsync(playerId, biome, ct);
            if (pedestals.Count == 0)
            {
                return new ShowcaseActionResultDto(
                    false,
                    $"Vous devez d'abord créer le biome « {GetBiomeLabel(biome)} » dans votre Vitrine 3D.",
                    null);
            }
            int targetGlobalSlot = EncodeGlobalSlotIndex(biome, localSlotIndex);
            var targetPedestal = pedestals.FirstOrDefault(p => p.SlotIndex == targetGlobalSlot);

            if (targetPedestal is not null)
            {
                targetPedestal.DisplayedPlayerCardId = null;
                await _db.SaveChangesAsync(ct);
            }

            var updatedView = await GetPlayerShowcaseCoreAsync(playerId, biome, ct);
            return new ShowcaseActionResultDto(
                true,
                $"Socle 3D #{localSlotIndex + 1} libéré.",
                updatedView);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ShowcaseActionResultDto> UpdatePedestalPositionAsync(
        Guid playerId,
        BiomeType biome,
        int localSlotIndex,
        double posX,
        double posY,
        double posZ,
        CancellationToken ct = default)
    {
        if (localSlotIndex < 0 || localSlotIndex >= PedestalsPerBiome)
        {
            return new ShowcaseActionResultDto(false, "Socle 3D introuvable.", null);
        }

        await _gate.WaitAsync(ct);
        try
        {
            var pedestals = await GetCreatedPedestalsForBiomeAsync(playerId, biome, ct);
            if (pedestals.Count == 0)
            {
                return new ShowcaseActionResultDto(
                    false,
                    $"Vous devez d'abord créer le biome « {GetBiomeLabel(biome)} » dans votre Vitrine 3D.",
                    null);
            }
            int targetGlobalSlot = EncodeGlobalSlotIndex(biome, localSlotIndex);
            var targetPedestal = pedestals.First(p => p.SlotIndex == targetGlobalSlot);

            targetPedestal.PositionX = Math.Round(Math.Clamp(posX, -2.65, 2.65), 2);
            targetPedestal.PositionY = Math.Round(Math.Clamp(posY, 0.15, 1.50), 2);
            targetPedestal.PositionZ = Math.Round(Math.Clamp(posZ, -2.65, 2.65), 2);

            await _db.SaveChangesAsync(ct);

            var updatedView = await GetPlayerShowcaseCoreAsync(playerId, biome, ct);
            return new ShowcaseActionResultDto(
                true,
                $"Position 3D du Socle #{localSlotIndex + 1} mise à jour ({targetPedestal.PositionX:F2}, {targetPedestal.PositionY:F2}, {targetPedestal.PositionZ:F2}).",
                updatedView);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ShowcaseActionResultDto> ResetPedestalPositionsAsync(
        Guid playerId,
        BiomeType biome,
        CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var pedestals = await GetCreatedPedestalsForBiomeAsync(playerId, biome, ct);
            if (pedestals.Count == 0)
            {
                return new ShowcaseActionResultDto(
                    false,
                    $"Vous devez d'abord créer le biome « {GetBiomeLabel(biome)} » dans votre Vitrine 3D.",
                    null);
            }
            for (int i = 0; i < PedestalsPerBiome; i++)
            {
                int globalSlot = EncodeGlobalSlotIndex(biome, i);
                var p = pedestals.FirstOrDefault(x => x.SlotIndex == globalSlot);
                if (p is not null)
                {
                    var defPos = DefaultPedestalClearings[i];
                    p.PositionX = defPos.X;
                    p.PositionY = defPos.Y;
                    p.PositionZ = defPos.Z;
                }
            }

            await _db.SaveChangesAsync(ct);

            var updatedView = await GetPlayerShowcaseCoreAsync(playerId, biome, ct);
            return new ShowcaseActionResultDto(
                true,
                "Emplacements des 5 socles 3D réinitialisés dans les clairières du biome.",
                updatedView);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<PlayerProfile> LoadPlayerWithCardsAsync(Guid playerId, CancellationToken ct)
    {
        var player = await _db.Players
            .Include(p => p.InventoryCards)
                .ThenInclude(pc => pc.SpeciesCard)
            .FirstOrDefaultAsync(p => p.Id == playerId, ct);

        if (player is null)
        {
            player = await _db.Players
                .Include(p => p.InventoryCards)
                    .ThenInclude(pc => pc.SpeciesCard)
                .OrderBy(p => p.CreatedAt)
                .FirstAsync(ct);
        }

        // Assure que chaque SpeciesCard possédée a bien son vrai Biome résolu en base
        bool speciesBiomeUpdated = false;
        foreach (var pc in player.InventoryCards)
        {
            if (pc.SpeciesCard is not null)
            {
                var resolved = ResolveCardBiome(pc.SpeciesCard);
                if (pc.SpeciesCard.Biome != resolved)
                {
                    pc.SpeciesCard.Biome = resolved;
                    speciesBiomeUpdated = true;
                }
            }
        }
        if (speciesBiomeUpdated)
        {
            await _db.SaveChangesAsync(ct);
        }

        return player;
    }

    private static List<BiomeSlotInfoDto> BuildBiomeSlotInfos(
        Dictionary<BiomeType, (string? BlendFile, string? GlbUrl, string? PreviewUrl, bool Available)> availableModels,
        List<ShowcasePedestal> allPlayerPedestals)
    {
        var biomeSlotInfos = new List<BiomeSlotInfoDto>();

        foreach (var def in AllBiomeDefinitions)
        {
            var (blendFile, glbUrl, previewUrl, isAvailable) = availableModels.GetValueOrDefault(def.Biome);
            int minSlot = EncodeGlobalSlotIndex(def.Biome, 0);
            int maxSlot = EncodeGlobalSlotIndex(def.Biome, PedestalsPerBiome - 1);

            var biomePedestals = allPlayerPedestals
                .Where(sp => sp.SlotIndex >= minSlot && sp.SlotIndex <= maxSlot)
                .ToList();

            bool isCreated = biomePedestals.Count > 0;
            int displayedInBiome = biomePedestals.Count(sp => sp.DisplayedPlayerCardId.HasValue);

            biomeSlotInfos.Add(new BiomeSlotInfoDto(
                def.Biome,
                def.Biome.ToString(),
                def.Label,
                def.CanonicalBlendFile,
                def.CanonicalGlbFile,
                blendFile,
                glbUrl,
                previewUrl,
                isAvailable,
                isCreated,
                displayedInBiome,
                PedestalsPerBiome,
                def.Description
            ));
        }

        return biomeSlotInfos;
    }

    private async Task<ShowcaseViewDto?> GetPlayerShowcaseCoreAsync(
        Guid playerId,
        BiomeType? selectedBiome,
        CancellationToken ct)
    {
        var player = await LoadPlayerWithCardsAsync(playerId, ct);
        var availableModels = ScanAvailableBiomeModels();

        var allPlayerPedestals = await _db.ShowcasePedestals
            .AsNoTracking()
            .Where(sp => sp.OwnerPlayerId == player.Id && sp.SlotIndex >= BaseSlotOffset)
            .ToListAsync(ct);

        var allInfos = BuildBiomeSlotInfos(availableModels, allPlayerPedestals);
        var createdBiomes = allInfos.Where(b => b.IsCreatedByPlayer).ToList();

        if (createdBiomes.Count == 0)
        {
            return null;
        }

        BiomeType activeBiome;
        if (selectedBiome.HasValue && createdBiomes.Any(b => b.Biome == selectedBiome.Value))
        {
            activeBiome = selectedBiome.Value;
        }
        else if (selectedBiome.HasValue)
        {
            return null;
        }
        else
        {
            activeBiome = createdBiomes[0].Biome;
        }

        var pedestals = await GetCreatedPedestalsForBiomeAsync(player.Id, activeBiome, ct);
        if (pedestals.Count == 0)
        {
            return null;
        }

        var inventoryDtos = player.InventoryCards
            .OrderByDescending(pc => pc.SpeciesCard != null ? (int)pc.SpeciesCard.Rarity : 0)
            .ThenByDescending(pc => pc.CurrentAttack)
            .ThenByDescending(pc => pc.Level)
            .Select(MapToPlayerCardDto)
            .ToList();

        var cardById = inventoryDtos.ToDictionary(c => c.Id);

        var pedestalDtos = new List<ShowcasePedestalDto>();
        for (int i = 0; i < PedestalsPerBiome; i++)
        {
            int globalSlot = EncodeGlobalSlotIndex(activeBiome, i);
            var entity = pedestals.First(p => p.SlotIndex == globalSlot);
            PlayerCardDto? displayedCard = entity.DisplayedPlayerCardId.HasValue &&
                                           cardById.TryGetValue(entity.DisplayedPlayerCardId.Value, out var dto)
                ? dto
                : null;

            pedestalDtos.Add(new ShowcasePedestalDto(
                entity.Id,
                i,
                globalSlot,
                DefaultPedestalClearings[i].Label,
                entity.PositionX,
                entity.PositionY,
                entity.PositionZ,
                displayedCard
            ));
        }

        var activeModelInfo = availableModels.GetValueOrDefault(activeBiome);

        return new ShowcaseViewDto(
            player.Id,
            player.Username,
            string.IsNullOrWhiteSpace(player.AvatarUrl)
                ? UserProfileService.BuildDefaultAvatarSvg(player.Username)
                : player.AvatarUrl,
            activeBiome,
            GetBiomeLabel(activeBiome),
            activeModelInfo.GlbUrl,
            activeModelInfo.PreviewUrl,
            activeModelInfo.BlendFile,
            activeModelInfo.Available,
            createdBiomes,
            pedestalDtos,
            inventoryDtos
        );
    }

    private async Task<List<ShowcasePedestal>> GetCreatedPedestalsForBiomeAsync(
        Guid playerId,
        BiomeType biome,
        CancellationToken ct)
    {
        int minSlot = EncodeGlobalSlotIndex(biome, 0);
        int maxSlot = EncodeGlobalSlotIndex(biome, PedestalsPerBiome - 1);

        return await _db.ShowcasePedestals
            .Where(sp => sp.OwnerPlayerId == playerId && sp.SlotIndex >= minSlot && sp.SlotIndex <= maxSlot)
            .OrderBy(sp => sp.SlotIndex)
            .ToListAsync(ct);
    }

    private static BiomeType ResolveCardBiome(SpeciesCard? species)
    {
        if (species is null) return BiomeType.Taiga;
        return ResolveBiomeForSpecies(species.ScientificName, species.CommonName, species.Latitude);
    }

    private static PlayerCardDto MapToPlayerCardDto(PlayerCard pc)
    {
        var species = pc.SpeciesCard;
        var rarityEnum = species?.Rarity ?? CardRarity.Commune;
        string sciName = species?.ScientificName ?? "Species incognita";
        var biomeEnum = ResolveCardBiome(species);

        return new PlayerCardDto(
            pc.Id,
            pc.SpeciesCardId,
            species?.CommonName ?? "Espèce inconnue",
            sciName,
            species?.Family ?? "Animalia",
            rarityEnum.ToString(),
            biomeEnum.ToString(),
            species?.ImageUrl ?? string.Empty,
            pc.CustomNickname,
            pc.Level,
            pc.ExperiencePoints,
            pc.Stage.ToString(),
            pc.State.ToString(),
            pc.IsInActiveDeck,
            pc.CurrentAttack,
            pc.CurrentDefense,
            pc.CurrentSpeed,
            pc.WeightKg,
            pc.PolaroidFinish,
            pc.ObtainedVia,
            pc.AcquiredAt,
            GachaService.RarityLabel(rarityEnum),
            GachaService.GetEstimatedNaturalOccurrences(sciName, rarityEnum),
            GachaService.DropRateForRarity(rarityEnum),
            GetBiomeLabel(biomeEnum)
        );
    }
}
