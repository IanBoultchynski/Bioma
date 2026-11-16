# Bioma — Backlog des User Stories (66 US · 13 Épiques · 205 Points)

Ce document recense l'intégralité des User Stories du projet **Bioma** et leur correspondance dans l'architecture du code afin de suivre l'avancement en binôme.

---

## Architecture préparée

| Couche | Fichiers / Modules préparés | Rôle |
| :--- | :--- | :--- |
| **IA / Reconnaissance (Python)** | `backend-python/app/animal_id.py` | Script CLIP zero-shot (`openai/clip-vit-base-patch32`) prêt pour les US 9, 10 et 11 |
| **Domaine C# (Enums)** | `backend-csharp/Bioma.Domain/Enums/GameEnums.cs` | Raretés, stades d'évolution, biomes, saisons, statuts d'expédition/enchère/combat |
| **Domaine C# (Entités)** | `backend-csharp/Bioma.Domain/Entities/DomainEntities.cs`<br>`backend-csharp/Bioma.Domain/Entities/BacklogEntities.cs` | `SpeciesCard` + modèles préparatoires des 13 épiques (`PlayerProfile`, `CaptureAttempt`, `ExpeditionMission`, `LaboratoryFusion`, `AuctionListing`, `BattleMatch`, etc.) |
| **Application C# (Contrats)** | `backend-csharp/Bioma.Application/Interfaces/IBiomaServices.cs`<br>`backend-csharp/Bioma.Application/Interfaces/IBacklogServices.cs` | Interfaces des services métiers découpées par épique |
| **Infrastructure & API C#** | `backend-csharp/Bioma.Infrastructure/`<br>`backend-csharp/Bioma.Api/` | Persistance EF Core, connecteurs scientifiques GBIF/iNaturalist et contrôleurs REST |
| **Frontend Blazor** | `frontend-blazor/Bioma.Client/` | Interface utilisateur interactive |

---

## Suivi des User Stories par Épique

### 1. Utilisateurs & Profil (15 pts) — ✅ Terminé
- [x] **US-01** *(3 pts)* : En tant que joueur, je souhaite me connecter de manière sécurisée afin de protéger mon compte et mes cartes.
- [x] **US-02** *(2 pts)* : En tant que joueur, je souhaite créer un profil utilisateur afin d'avoir une identité unique dans le jeu.
- [x] **US-03** *(2 pts)* : En tant que joueur, je souhaite consulter mon deck et mon inventaire afin de voir la liste exacte des cartes que je possède.
- [x] **US-04** *(5 pts)* : En tant que joueur, je souhaite que ma progression soit automatiquement sauvegardée sur le cloud afin de ne perdre aucune de mes données si je change de téléphone.
- [x] **US-05** *(1 pt)* : En tant que joueur, je souhaite modifier mon avatar de profil afin de personnaliser mon compte.
- [x] **US-06** *(2 pts)* : En tant que joueur, je souhaite consulter mes statistiques globales (nombre de cartes, victoires) afin de suivre ma progression globale.

### 2. Capture IRL (31 pts)
- [ ] **US-07** *(2 pts)* : En tant que joueur, je souhaite ouvrir le module "Instantané" et autoriser l'accès à ma caméra afin de prendre des photos depuis l'application.
- [ ] **US-08** *(3 pts)* : En tant que joueur, je souhaite que ma position géographique soit enregistrée lors d'une photo afin de lier l'animal capturé à son biome réel.
- [ ] **US-09** *(8 pts)* : En tant que joueur, je souhaite que l'application identifie automatiquement l'animal photographié afin de débloquer sa carte virtuelle correspondante *(via `animal_id.py`)*.
- [ ] **US-10** *(2 pts)* : En tant que joueur, je souhaite être averti si l'animal n'est pas reconnu (flou, mauvaise lumière) afin de pouvoir reprendre ma photo.
- [ ] **US-11** *(3 pts)* : En tant que joueur, je souhaite être invité à reprendre ma photo sous un meilleur angle si l'application hésite entre plusieurs espèces afin d'assurer l'exactitude de ma collection sans permettre la triche.
- [ ] **US-12** *(3 pts)* : En tant que joueur, je souhaite que les statistiques de ma nouvelle carte (poids, habitat) reflètent les véritables données scientifiques de l'espèce afin d'apprendre des choses sur l'animal.
- [ ] **US-13** *(3 pts)* : En tant que joueur, je souhaite voir une animation d'impression de style "Polaroïd" lors de l'obtention de ma carte afin d'avoir un retour visuel gratifiant.
- [ ] **US-14** *(2 pts)* : En tant que joueur, je souhaite recevoir de la monnaie virtuelle ou de l'XP si je photographie un animal que je possède déjà afin que mon effort soit toujours récompensé.
- [ ] **US-15** *(5 pts)* : En tant que joueur, je souhaite que ma photo soit sauvegardée temporairement si je n'ai pas de réseau afin de ne pas perdre ma capture lors d'une balade en nature.

### 3. Expéditions Data (23 pts)
- [ ] **US-16** *(5 pts)* : En tant que joueur, je souhaite visualiser un globe 3D interactif afin d'explorer la carte du monde du jeu.
- [ ] **US-17** *(5 pts)* : En tant que joueur, je souhaite voir des zones lumineuses (hotspots) sur le globe afin d'identifier où se concentrent les différentes espèces.
- [ ] **US-18** *(3 pts)* : En tant que joueur, je souhaite envoyer mes cartes animales dans des zones d'expédition spécifiques afin de trouver des espèces.
- [ ] **US-19** *(5 pts)* : En tant que joueur, je souhaite que les expéditions disponibles ne soient ouvertes qu'aux véritables saisons d'apparition de l'animal afin de renforcer le réalisme scientifique.
- [ ] **US-20** *(3 pts)* : En tant que joueur, je souhaite recevoir une notification de succès ou d'échec à la fin de l'expédition afin de découvrir mes récompenses.
- [ ] **US-21** *(2 pts)* : En tant que joueur, je souhaite pouvoir annuler une expédition en cours afin de récupérer mon animal si j'en ai besoin pour combattre.

### 4. Quiz Chronométré (10 pts)
- [ ] **US-22** *(2 pts)* : En tant que joueur, je souhaite participer à des événements de quiz portant sur la biologie afin de tester mes connaissances.
- [ ] **US-23** *(3 pts)* : En tant que joueur, je souhaite que le quiz soit chronométré de manière stricte et équitable afin d'ajouter du défi à ma réflexion.
- [ ] **US-24** *(2 pts)* : En tant que joueur, je souhaite valider ma réponse afin de savoir instantanément si j'ai trouvé la bonne information.
- [ ] **US-25** *(2 pts)* : En tant que joueur, je souhaite gagner de la monnaie virtuelle ou de l'XP en cas de bonne réponse afin de m'encourager à apprendre.
- [ ] **US-26** *(1 pt)* : En tant que joueur, je souhaite rater la récompense et voir une animation d'erreur si je dépasse le temps imparti afin de comprendre mon échec.

### 5. Tirage (Gacha) (14 pts)
- [ ] **US-27** *(2 pts)* : En tant que joueur, je souhaite dépenser ma monnaie virtuelle afin de déclencher un tirage de carte aléatoire.
- [ ] **US-28** *(5 pts)* : En tant que joueur, je souhaite que les animaux rares dans la vraie nature soient les plus difficiles à obtenir au tirage afin de valoriser mes meilleures cartes.
- [ ] **US-29** *(3 pts)* : En tant que joueur, je souhaite voir une animation de loterie dévoilant ma nouvelle carte afin de ressentir du suspense.
- [ ] **US-30** *(2 pts)* : En tant que joueur, je souhaite que ma nouvelle carte indique clairement son niveau de rareté (Commune, Rare, Légendaire) afin d'évaluer sa valeur.
- [ ] **US-31** *(2 pts)* : En tant que joueur, je souhaite pouvoir lancer plusieurs tirages d'un coup (ex: x10) afin de gagner du temps lorsque je suis riche.

### 6. Progression (13 pts)
- [ ] **US-32** *(3 pts)* : En tant que joueur, je souhaite faire gagner de l'expérience à mes animaux via des activités (quiz, expéditions) afin de les faire progresser.
- [ ] **US-33** *(3 pts)* : En tant que joueur, je souhaite que mon animal évolue (ex: stade juvénile vers adulte) lorsqu'il atteint un seuil d'XP afin de marquer sa croissance.
- [ ] **US-34** *(2 pts)* : En tant que joueur, je souhaite que les statistiques de combat de mon animal soient améliorées après une évolution afin de le rendre plus compétitif.
- [ ] **US-35** *(3 pts)* : En tant que joueur, je souhaite que le cadre Polaroïd de ma carte obtienne une finition visuelle distincte après son évolution afin de pouvoir frimer.
- [ ] **US-36** *(2 pts)* : En tant que joueur, je souhaite pouvoir appliquer des "soins virtuels" quotidiens à mes animaux afin de leur octroyer un léger bonus d'expérience.

### 7. Le Laboratoire (30 pts)
- [ ] **US-37** *(1 pt)* : En tant que joueur, je souhaite accéder à un Laboratoire afin de pouvoir tenter de croiser deux espèces.
- [ ] **US-38** *(2 pts)* : En tant que joueur, je souhaite pouvoir placer deux de mes cartes "Adulte" distinctes dans les emplacements de fusion afin de préparer l'expérience.
- [ ] **US-39** *(1 pt)* : En tant que joueur, je souhaite être empêché de fusionner deux fois la même carte (bouton grisé) afin de ne pas gâcher mes ressources par erreur.
- [ ] **US-40** *(1 pt)* : En tant que joueur, je souhaite dépenser de l'énergie ou de la monnaie afin de valider le lancement de la création hybride.
- [ ] **US-41** *(3 pts)* : En tant que joueur, je souhaite pouvoir ajouter un objet "Boost" catalysant avant la fusion afin de garantir des statistiques supérieures à ma nouvelle créature (+10%).
- [ ] **US-42** *(5 pts)* : En tant que joueur, je souhaite que ma créature hybride hérite d'un mélange équilibré des statistiques de ses deux parents afin d'être utile en combat.
- [ ] **US-43** *(8 pts)* : En tant que joueur, je souhaite obtenir une image unique générée par IA pour ma créature hybride afin de posséder une carte qui n'existe nulle part ailleurs.
- [ ] **US-44** *(3 pts)* : En tant que joueur, je souhaite que mes ressources me soient rendues avec un message d'excuse si le laboratoire est surchargé afin de ne pas être lésé par un problème technique.
- [ ] **US-45** *(3 pts)* : En tant que joueur, je souhaite voir ma nouvelle carte hybride s'ajouter définitivement à mon inventaire à la fin du processus afin de pouvoir la jouer.
- [ ] **US-46** *(3 pts)* : En tant que joueur, je souhaite voir les deux cartes parentes être "consommées" ou "mises au repos" après la fusion afin d'équilibrer mon deck.

### 8. Marché aux Enchères (19 pts)
- [ ] **US-47** *(2 pts)* : En tant que joueur, je souhaite accéder à un hôtel de vente afin de pouvoir vendre ou acheter des cartes aux autres joueurs.
- [ ] **US-48** *(3 pts)* : En tant que joueur, je souhaite mettre aux enchères mes cartes en double ou mes hybrides afin de gagner de la monnaie.
- [ ] **US-49** *(8 pts)* : En tant que joueur, je souhaite voir le prix et le temps restant des enchères s'actualiser en direct sous mes yeux afin de pouvoir réagir instantanément sans rafraîchir la page.
- [ ] **US-50** *(3 pts)* : En tant que joueur, je souhaite pouvoir placer une surenchère sur une carte qui m'intéresse afin d'espérer la remporter.
- [ ] **US-51** *(3 pts)* : En tant que joueur, je souhaite être remboursé immédiatement de ma mise si quelqu'un surenchérit sur moi afin de pouvoir enchérir à nouveau.

### 9. Social & Échanges (9 pts)
- [ ] **US-52** *(2 pts)* : En tant que joueur, je souhaite pouvoir ajouter des profils à ma liste d'amis afin de garder le contact.
- [ ] **US-53** *(5 pts)* : En tant que joueur, je souhaite proposer un échange sécurisé de cartes avec un ami afin de s'entraider dans nos collections.
- [ ] **US-54** *(2 pts)* : En tant que joueur, je souhaite envoyer directement un cadeau (carte ou objet) à un ami afin de lui faire plaisir.

### 10. Batailles 1v1 (20 pts)
- [ ] **US-55** *(5 pts)* : En tant que joueur, je souhaite rechercher un adversaire en ligne afin de participer à un duel tactique.
- [ ] **US-56** *(2 pts)* : En tant que joueur, je souhaite sélectionner les cartes actives de mon deck que je souhaite utiliser afin de préparer ma stratégie de combat.
- [ ] **US-57** *(8 pts)* : En tant que joueur, je souhaite que l'issue de mon combat dépende du poids, de la dangerosité et du biome réel de mon animal afin que le réalisme scientifique ait un vrai impact tactique.
- [ ] **US-58** *(2 pts)* : En tant que joueur, je souhaite voir mon classement (score Elo) augmenter en cas de victoire et baisser en cas de défaite afin de me mesurer aux autres joueurs.
- [ ] **US-59** *(2 pts)* : En tant que joueur, je souhaite visualiser une animation claire de victoire ou de défaite à la fin du combat afin d'avoir un retour immersif.
- [ ] **US-60** *(1 pt)* : En tant que joueur, je souhaite consulter un historique de mes précédents matchs afin d'analyser mes résultats.

### 11. Vitrine 3D (9 pts)
- [ ] **US-61** *(3 pts)* : En tant que joueur, je souhaite accéder à mon terrarium/musée virtuel (Vitrine 3D) afin de profiter d'un espace personnel de contemplation.
- [ ] **US-62** *(3 pts)* : En tant que joueur, je souhaite pouvoir positionner et exposer mes plus belles cartes sur des socles 3D afin de mettre en valeur mes prises.
- [ ] **US-63** *(3 pts)* : En tant que joueur, je souhaite pouvoir visiter la Vitrine 3D d'autres joueurs afin d'admirer leurs créatures rares ou hybrides.

### 12. Réalisme Data (7 pts)
- [ ] **US-64** *(2 pts)* : En tant que joueur, je souhaite que les fiches des cartes que je collectionne affichent des descriptions biologiques véridiques afin de m'instruire.
- [ ] **US-65** *(5 pts)* : En tant que joueur, je souhaite que les photos générées lors des expéditions soient toujours cohérentes avec l'environnement de l'animal (ex: pas de pingouin dans le désert) afin de maintenir l'immersion éducative.

### 13. Paramètres (2 pts)
- [ ] **US-66** *(2 pts)* : En tant que joueur, je souhaite pouvoir activer ou désactiver les notifications push de l'application afin de ne pas être dérangé de façon intempestive.
