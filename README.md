<h1 align="center">ResuME</h1>

<p align="center">
  <strong>Un CV que l'on traverse.</strong><br/>
  Mini RPG 2D pixel art en vue 3/4 : le recruteur parcourt les étapes du parcours d'Éric Dovergne et remplit sa fiche de personnage.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/version-0.1.0-E6B422?style=flat-square" alt="version" />
  <img src="https://img.shields.io/badge/statut-prototype-orange?style=flat-square" alt="statut" />
  <img src="https://img.shields.io/badge/Unity-6000.3.23f1-000000?style=flat-square&logo=unity" alt="Unity" />
  <img src="https://img.shields.io/badge/C%23-9-239120?style=flat-square&logo=csharp" alt="C#" />
  <img src="https://img.shields.io/badge/licence-propri%C3%A9taire-6B6B6B?style=flat-square" alt="licence" />
</p>

---

## Sommaire

- [Vue d'ensemble](#vue-densemble)
- [Fonctionnalités](#fonctionnalités)
- [Stack technique](#stack-technique)
- [Structure du dépôt](#structure-du-dépôt)
- [Installation locale](#installation-locale)
- [Versionnage & branches](#versionnage--branches)
- [Documentation](#documentation)
- [Licence](#licence)

---

## Vue d'ensemble

Un CV se lit en trente secondes et s'oublie aussi vite. **ResuME** le transforme en un petit
jeu que l'on traverse : le recruteur incarne Éric depuis sa chambre d'enfant, reçoit une quête
légère au lycée, passe par chaque école et chaque entreprise du parcours et remplit une fiche
de personnage (classe, compétences, traits, passif). Arrivé à aujourd'hui, une pluie de
compétences écrase le personnage ; un pouce en sort et sert de bouton de contact.

Le jeu est pensé pour des non-joueurs : défilement aux flèches ou au swipe, aucune interaction
bloquante, aucun échec, trois à cinq minutes de bout en bout. Les visiteurs simultanés se
voient, sans qu'aucune trace ne soit conservée.

| | |
|---|---|
| **Projet** | ResuME |
| **Studio** | Goras Games Studio |
| **Phase** | Phase 2 - Prototype greybox |
| **Cible** | WebGL (navigateur, bureau et mobile en paysage) |

---

## Fonctionnalités

### En place

| Brique | Description |
|---|---|
| **Projet Unity 6.3** | Gabarit Universal 2D (URP, 2D Renderer), Input System, GGS Core |

### À venir

| Brique | Dépend de |
|---|---|
| **Déplacement au défilement** (flèches, swipe), vue 3/4 sur une voie unique | Input Actions |
| **Bâtiments traversables** (murs qui disparaissent), façades, guichet, stand, place | Greybox des tailles |
| **Fiche de personnage** : classe, compétences, traits, passif, niveau = âge | HUD |
| **Contenu** : quinze arrêts, PNJ, quête, fin (pluie, pouce, contact) | Direction artistique |
| **Présence multijoueur** (WebSocket, pseudos générés, aucune trace) | Serveur de présence |
| **Menu et CV PDF**, déploiement web | Build WebGL |

---

## Stack technique

| Couche | Technologie | Version |
|---|---|---|
| Moteur | Unity | 6000.3.23f1 (6.3 LTS) |
| Rendu | Universal Render Pipeline, 2D Renderer | 17.3 |
| Entrées | Input System | 1.20 |
| Socle studio | GGS Core | 0.7.0 |
| Cible | WebGL | build compressé Brotli |

---

## Structure du dépôt

```
ResuME/
├── Assets/
│   ├── Scenes/            ← scènes du jeu
│   └── Settings/          ← URP, 2D Renderer, Input Actions
├── Packages/              ← manifest des packages (dont GGS Core)
├── ProjectSettings/       ← réglages projet, GGSVersion.json (source de vérité de la version)
├── LICENSE
└── README.md
```

Les documents de conception et de suivi (concept, fiches par étape, décisions) sont des
documents internes, hors dépôt.

---

## Installation locale

Prérequis : Unity Hub avec l'éditeur **6000.3.23f1**, Git et Git LFS, accès au dépôt privé
GGS Core.

```bash
git lfs install
```

```bash
git clone git@github.com:GorasGamesStudio/ResuME.git
```

Ouvrir le dossier `ResuME` dans Unity Hub avec l'éditeur 6000.3.23f1. Les packages se
résolvent à l'ouverture.

---

## Versionnage & branches

Format jeu `X.Y.Z.B`, source de vérité `ProjectSettings/GGSVersion.json`.

| Segment | Déclenché par |
|---|---|
| **X** | Sortie publique, refonte, rupture de compatibilité |
| **Y** | Nouveau contenu ou fonctionnalité ; jalons de production avant 1.0 |
| **Z** | Correctif, ajustement, refactoring sans impact |
| **B** | Chaque build produit, compteur global jamais remis à zéro |

Branche `main` = version livrée. Une branche par brique (`feat/...`, `assets/...`), fusionnée
dans `main` par squash merge. Tag `vX.Y.Z` sur chaque commit de version.

---

## Documentation

| Document | Contenu |
|---|---|
| Conception | Concept, fiches par étape du parcours, décisions : documents internes, hors dépôt |

---

## Licence

Propriétaire, tous droits réservés. Voir [LICENSE](LICENSE).
