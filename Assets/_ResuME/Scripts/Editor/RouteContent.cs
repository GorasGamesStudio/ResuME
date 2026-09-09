using System.Collections.Generic;
using ResuME.Data;

namespace ResuME.Editor
{
    /// <summary>
    /// The fifteen stops of the route, transcribed from the design sheets. Regenerating the
    /// Route asset from here keeps the greybox in sync with the CV without hand-editing.
    /// </summary>
    public static class RouteContent
    {
        private static RewardDefinition Skill(string id, string label, SkillCategory category, string description) =>
            new RewardDefinition { kind = RewardKind.Skill, id = id, label = label, category = category, description = description };

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
            const SkillCategory T = SkillCategory.Technique;
            const SkillCategory G = SkillCategory.Gestion;

            return new List<StopDefinition>
            {
                Stop("chambre", "Chambre d'enfant", "vers 1991", 6, StopKind.Intro, 1.0f,
                    notes: "NES, pistolet Nintendo, la télé sert d'écran de chargement.",
                    road: new[] { Trait("curiosite", "Curiosité", "Tout a commencé devant une NES.") }),

                Stop("bac", "Lycée Saint Vincent, Senlis", "2003 · BAC STT Comptabilité & Gestion", 18, StopKind.Facade, 0.6f,
                    rewards: new[]
                    {
                        Skill("comptabilite", "Comptabilité générale", T, "Bases en comptabilité et gestion administrative."),
                        Class("classe-gestionnaire", "Gestionnaire", "BAC STT Comptabilité & Gestion, 2003.")
                    },
                    notes: "Le PNJ donne la quête."),

                Stop("bts", "Lycée Saint Vincent, Senlis", "2005 · BTS Informatique de Gestion", 20, StopKind.Facade, 0.6f,
                    rewards: new[]
                    {
                        Skill("c", "C", T, "Algorithmie, bases de données, Visual Basic, Merise et UML."),
                        Skill("etude-projet", "Étude de projet", G, "Modélisation et architectures logicielles."),
                        Class("classe-dev-app", "Développeur d'applications", "BTS Informatique de Gestion, 2005.")
                    },
                    notes: "Même bâtiment que le BAC, un étage en plus."),

                Stop("support", "Support et développement informatique", "2005 - 2010", 20, StopKind.Traverse, 0.8f,
                    rewards: new[]
                    {
                        Skill("maintenance", "Maintenance & Support informatique", T, "Support utilisateurs, configuration de postes, maintenance réseau."),
                        Skill("installation", "Installation matériel informatique", T, "Installation et migration de parcs informatiques.")
                    },
                    road: new[] { Trait("sens-service", "Sens du service", "Support technique sur site, au contact des utilisateurs.") },
                    notes: "Plusieurs entreprises, traitées en bâtiment anonyme."),

                Stop("creajeux", "Créajeux, Nîmes", "2010 - 2013 · Programmation de jeux vidéo (RNCP 6)", 25, StopKind.Facade, 0.8f,
                    rewards: new[]
                    {
                        Skill("unity", "Unity 3D", T, "Prototypes 2D et 3D, physique temps réel, gameplay en équipe."),
                        Skill("csharp", "C#", T, "Programmation orientée objet en C#."),
                        Skill("cpp", "C++", T, "Programmation orientée objet en C++."),
                        Skill("poo", "Programmation orientée objet", T, "C, C++, C#."),
                        Skill("game-design", "Conception de jeux vidéo", T, "Game design, game jams, projets personnels."),
                        Class("classe-dev-jeux", "Développeur de jeux vidéo", "Créajeux, 2013. Classe définitive.")
                    }),

                Stop("montbeliard", "Développement Unity, Montbéliard", "2013 - 2016", 28, StopKind.Traverse, 1.2f,
                    rewards: new[] { Skill("vr", "Réalité virtuelle", T, "Applications 3D pédagogiques, culturelles et immersives pour l'armée et le tourisme.") },
                    road: new[] { Trait("esprit-equipe", "Esprit d'équipe", "Premiers projets professionnels, en équipe.") }),

                Stop("alsace", "Développement pharmaceutique, Alsace", "2017", 32, StopKind.Traverse, 0.8f,
                    rewards: new[] { Skill("optimisation", "Analyse & Optimisation de processus", G, "Serious games de simulation et d'apprentissage.") }),

                Stop("ggs", "Goras Games Studio", "2018 - aujourd'hui · micro-entreprise", 33, StopKind.Counter, 0.4f, founder: true,
                    rewards: new[]
                    {
                        Passive("ggs-passif", "Goras Games Studio", "Actif depuis 2018 : jeux PC, mobile et VR, applications web, multijoueur. Docker, Notion, GitHub."),
                        Skill("pilotage", "Pilotage de projets", G, "Conception et développement de jeux et d'applications.")
                    },
                    road: new[] { Trait("autonomie", "Autonomie", "Micro-entreprise menée en parallèle de tout le reste.") }),

                Stop("game-academy", "Game Academy", "2018 - 2020 · Formateur", 33, StopKind.Traverse, 1.2f,
                    rewards: new[] { Skill("formation", "Formation & Accompagnement", G, "Programme pédagogique sur quatre ans, de la prépa à la 3e année.") },
                    road: new[] { Trait("force-proposition", "Force de proposition", "Événements, salons jeux vidéo, apport d'idées nouvelles.") }),

                Stop("rpaint", "R'Paint Games", "2020 - 2023 · Co-fondateur & Président", 35, StopKind.Traverse, 1.2f, founder: true,
                    rewards: new[]
                    {
                        Skill("creation-entreprise", "Création & Gestion d'entreprise", G, "Loisirs sportifs et activités récréatives."),
                        Skill("coordination", "Coordination d'équipe", G, "Gestion administrative, logistique et comptable.")
                    },
                    notes: "Le bâtiment se rénove pendant la traversée."),

                Stop("lidl", "Lidl", "2023 - 2024 · Employé polyvalent", 38, StopKind.Traverse, 0.8f,
                    rewards: new[] { Skill("ouverture", "Ouverture de nouvel établissement", G, "Préparation et ouverture d'un nouveau magasin.") },
                    road: new[] { Trait("polyvalence", "Polyvalence", "Rayons, boulangerie, inventaire, encaissement.") }),

                Stop("loire-games", "Loire Games", "2025 - aujourd'hui · Co-fondateur & Trésorier", 40, StopKind.Stand, 0.6f, founder: true,
                    notes: "Stand de salon, jaune bien visible, logo Loire Games."),

                Stop("cine-sologne", "Ciné Sologne", "2025 - 2026 · Assistant directeur", 40, StopKind.Facade, 0.5f,
                    notes: "Façade seulement, sans logo."),

                Stop("crn", "Conseil Régional du Numérique Centre-Val de Loire", "2026 - aujourd'hui · Bénévolat", 40, StopKind.Square, 0.8f,
                    notes: "Place publique, logo du Conseil Régional."),

                Stop("aujourdhui", "Aujourd'hui", "2026", 40, StopKind.End, 1.0f,
                    notes: "Le PNJ regarde la caméra, pluie de compétences, pouce, carte de contact.")
            };
        }
    }
}
