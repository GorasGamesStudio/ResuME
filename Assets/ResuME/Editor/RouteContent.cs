using System.Collections.Generic;
using ResuME.Data;

namespace ResuME.Editor
{
    /// <summary>
    /// The fifteen stops of the route, transcribed from the design sheets. A skill is granted one
    /// mastery point by every project or experience that used it, so the same skill id appears
    /// under several stops. Regenerating the Route asset from here keeps the greybox in sync.
    /// </summary>
    public static class RouteContent
    {
        private const SkillCategory T = SkillCategory.Technique;
        private const SkillCategory G = SkillCategory.Gestion;

        /// <summary>Skill catalogue: id → label, category, description. Points are granted per stop.</summary>
        private static readonly Dictionary<string, (string label, SkillCategory category, string description)> Catalogue =
            new Dictionary<string, (string, SkillCategory, string)>
            {
                ["unity"] = ("Unity 3D", T, "Jeux et applications 3D, VR, prototypes 2D et 3D."),
                ["csharp"] = ("C#", T, "Langage principal, d'Unity aux outils web."),
                ["cpp"] = ("C++", T, "Programmation orientée objet en C++."),
                ["c"] = ("C", T, "Algorithmie et premiers programmes."),
                ["poo"] = ("Programmation orientée objet", T, "C, C++, C#, architectures logicielles."),
                ["game-design"] = ("Conception de jeux vidéo", T, "Game design, game jams, projets personnels et professionnels."),
                ["vr"] = ("Réalité virtuelle", T, "Applications immersives et expériences VR."),
                ["maintenance"] = ("Maintenance & Support informatique", T, "Support utilisateurs, configuration de postes, maintenance réseau."),
                ["installation"] = ("Installation matériel informatique", T, "Installation et migration de parcs, support technique sur site."),
                ["comptabilite"] = ("Comptabilité générale", T, "Comptabilité, gestion administrative et bancaire."),
                ["pilotage"] = ("Pilotage de projets", G, "Conception, planification et suivi de projets."),
                ["creation-entreprise"] = ("Création & Gestion d'entreprise", G, "Création et gestion de structures : micro-entreprise, société, association."),
                ["optimisation"] = ("Analyse & Optimisation de processus", G, "Analyse des besoins, procédures, automatisation."),
                ["etude-projet"] = ("Étude de projet", G, "Étude des besoins, modélisation, choix de solutions."),
                ["ouverture"] = ("Ouverture de nouvel établissement", G, "Préparation et ouverture d'une nouvelle structure."),
                ["formation"] = ("Formation & Accompagnement", G, "Pédagogie, programmes de formation, transmission."),
                ["coordination"] = ("Coordination d'équipe", G, "Organisation quotidienne et coordination d'équipes.")
            };

        private static RewardDefinition Gain(string id, int points = 1)
        {
            (string label, SkillCategory category, string description) entry = Catalogue[id];
            return new RewardDefinition { kind = RewardKind.Skill, id = id, label = entry.label, category = entry.category, description = entry.description, points = points };
        }

        private static RewardDefinition Trait(string id, string label, string description) =>
            new RewardDefinition { kind = RewardKind.Trait, id = id, label = label, category = SkillCategory.Relationnel, description = description };

        private static RewardDefinition Class(string id, string title, string description) =>
            new RewardDefinition { kind = RewardKind.ClassChange, id = id, label = title, description = description };

        private static RewardDefinition Passive(string id, string label, string description) =>
            new RewardDefinition { kind = RewardKind.Passive, id = id, label = label, description = description };

        private static StopDefinition Stop(string id, string title, string years, int level, StopKind kind, float width,
            bool founder = false, RewardDefinition[] rewards = null, RewardDefinition[] road = null, string notes = "")
        {
            StopDefinition stop = new StopDefinition
            {
                id = id, title = title, years = years, level = level, kind = kind, widthScreens = width, founder = founder, notes = notes
            };
            if (rewards != null) stop.rewards.AddRange(rewards);
            if (road != null) stop.roadRewards.AddRange(road);
            return stop;
        }

        public static List<StopDefinition> Build()
        {
            return new List<StopDefinition>
            {
                Stop("chambre", "Chambre d'enfant", "vers 1991", 6, StopKind.Intro, 1.0f,
                    notes: "NES, pistolet Nintendo, la télé sert d'écran de chargement.",
                    road: new[] { Trait("curiosite", "Curiosité", "Tout a commencé devant une NES.") }),

                Stop("bac", "Lycée Saint Vincent, Senlis", "2003 · BAC STT Comptabilité & Gestion", 18, StopKind.Facade, 0.6f,
                    rewards: new[]
                    {
                        Gain("comptabilite"),
                        Class("classe-gestionnaire", "Gestionnaire", "BAC STT Comptabilité & Gestion, 2003.")
                    },
                    notes: "Le PNJ donne la quête."),

                Stop("bts", "Lycée Saint Vincent, Senlis", "2005 · BTS Informatique de Gestion", 20, StopKind.Facade, 0.6f,
                    rewards: new[]
                    {
                        Gain("c"),
                        Gain("etude-projet"),
                        Class("classe-dev-app", "Développeur d'applications", "BTS Informatique de Gestion, 2005.")
                    },
                    notes: "Même bâtiment que le BAC, un étage en plus."),

                Stop("support", "Support et développement informatique", "2005 - 2010", 20, StopKind.Traverse, 0.8f,
                    rewards: new[] { Gain("maintenance"), Gain("installation") },
                    road: new[] { Trait("sens-service", "Sens du service", "Support technique sur site, au contact des utilisateurs.") },
                    notes: "Plusieurs entreprises, traitées en bâtiment anonyme."),

                Stop("creajeux", "Créajeux, Nîmes", "2010 - 2013 · Programmation de jeux vidéo (RNCP 6)", 25, StopKind.Facade, 0.8f,
                    rewards: new[]
                    {
                        Gain("unity"), Gain("csharp"), Gain("cpp"), Gain("c"), Gain("poo"), Gain("game-design"),
                        Class("classe-dev-jeux", "Développeur de jeux vidéo", "Créajeux, 2013. Classe définitive.")
                    }),

                Stop("montbeliard", "Développement Unity, Montbéliard", "2013 - 2016", 28, StopKind.Traverse, 1.2f,
                    rewards: new[] { Gain("unity"), Gain("csharp"), Gain("poo"), Gain("game-design"), Gain("vr"), Gain("pilotage"), Gain("etude-projet") },
                    road: new[] { Trait("esprit-equipe", "Esprit d'équipe", "Premiers projets professionnels, en équipe.") },
                    notes: "Applications 3D pour l'armée et le tourisme."),

                Stop("alsace", "Développement pharmaceutique, Alsace", "2017", 32, StopKind.Traverse, 0.8f,
                    rewards: new[] { Gain("unity"), Gain("csharp"), Gain("poo"), Gain("optimisation") }),

                Stop("ggs", "Goras Games Studio", "2018 - aujourd'hui · micro-entreprise", 33, StopKind.Counter, 0.4f, founder: true,
                    rewards: new[]
                    {
                        Passive("ggs-passif", "Goras Games Studio", "Actif depuis 2018 : jeux PC, mobile et VR, applications web, multijoueur. Docker, Notion, GitHub."),
                        Gain("unity"), Gain("csharp"), Gain("poo"), Gain("game-design"), Gain("vr"), Gain("pilotage"), Gain("creation-entreprise"), Gain("comptabilite")
                    },
                    road: new[] { Trait("autonomie", "Autonomie", "Micro-entreprise menée en parallèle de tout le reste.") }),

                Stop("game-academy", "Game Academy", "2018 - 2020 · Formateur", 33, StopKind.Traverse, 1.2f,
                    rewards: new[] { Gain("formation"), Gain("unity"), Gain("csharp"), Gain("poo"), Gain("game-design"), Gain("pilotage"), Gain("coordination") },
                    road: new[] { Trait("force-proposition", "Force de proposition", "Événements, salons jeux vidéo, apport d'idées nouvelles.") }),

                Stop("rpaint", "R'Paint Games", "2020 - 2023 · Co-fondateur & Président", 35, StopKind.Traverse, 1.2f, founder: true,
                    rewards: new[] { Gain("creation-entreprise"), Gain("coordination"), Gain("comptabilite"), Gain("pilotage"), Gain("ouverture") },
                    notes: "Le bâtiment se rénove pendant la traversée."),

                Stop("lidl", "Lidl", "2023 - 2024 · Employé polyvalent", 38, StopKind.Traverse, 0.8f,
                    rewards: new[] { Gain("ouverture"), Gain("optimisation") },
                    road: new[] { Trait("polyvalence", "Polyvalence", "Rayons, boulangerie, inventaire, encaissement.") }),

                Stop("loire-games", "Loire Games", "2025 - aujourd'hui · Co-fondateur & Trésorier", 40, StopKind.Stand, 0.6f, founder: true,
                    rewards: new[] { Gain("comptabilite"), Gain("creation-entreprise"), Gain("optimisation"), Gain("pilotage"), Gain("coordination"), Gain("csharp") },
                    notes: "Stand de salon, jaune bien visible, logo Loire Games."),

                Stop("cine-sologne", "Ciné Sologne", "2025 - 2026 · Assistant directeur", 40, StopKind.Facade, 0.5f,
                    rewards: new[] { Gain("maintenance"), Gain("installation"), Gain("coordination") },
                    notes: "Façade seulement, sans logo."),

                Stop("crn", "Conseil Régional du Numérique Centre-Val de Loire", "2026 - aujourd'hui · Bénévolat", 40, StopKind.Square, 0.8f,
                    rewards: new[] { Gain("optimisation"), Gain("etude-projet"), Gain("formation") },
                    notes: "Place publique, logo du Conseil Régional."),

                Stop("aujourdhui", "Aujourd'hui", "2026", 40, StopKind.End, 1.0f,
                    notes: "Le PNJ regarde la caméra, pluie de compétences, pouce, carte de contact.")
            };
        }
    }
}
