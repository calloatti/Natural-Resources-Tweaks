Include ..\AGENTS.md

# Natural Resources Tweaks — Mod-Specific Agent Instructions

## Identity
- **Assembly:** `naturalresourcestweaks`
- **Namespace:** `Calloatti.NaturalResourcesTweaks`
- **Framework:** Harmony, Bindito DI
- **ModId:** `Calloatti.NaturalResourcesTweaks`
- **Min Game Version:** 1.0.12.5 — uses `timberborn-decompiled-1.0.*`

## What This Mod Does
Replaces oversized mature ghost models of trees/crops/bushes with seedling variants during planting preview. Also includes mixed forest tool, beehive patches, range display, cutting/planting modifications, and planting bug fixes.

## Source Architecture (`Version-1.0/Source/`)

| File | Role |
|---|---|
| `ModStarter.cs` | Entry point — `IModStarter` |
| `ModConfigurator.cs` | DI configurator |
| `MixedForestTool.cs` | Mixed forest planting tool |
| `MixedForestTool.SimpleForest.cs` | Simple forest planting helper |
| `MixedForestButtonAdder.cs` | UI button injection for mixed forest |
| `PatchPlanting.cs` | Harmony patches for planting behavior |
| `PatchCutting.cs` | Harmony patches for cutting behavior |
| `PatchRangeDisplay.cs` | Range display patches |
| `BeehivePatch.cs` | Beehive behavior patches |
| `PlantingBugFix.cs` | Bug fixes for planting logic |
| `SharedUI.cs` | Shared UI utilities |

## Hard Rule
DO NOT EVER TOUCH THE DEPLOY FOLDER.

BUILD DOES EVERYTHING, NEVER EVER MESS WITH THE DEPLOY PROCESS.
