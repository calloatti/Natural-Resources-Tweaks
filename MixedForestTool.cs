using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.BlockSystem;
using Timberborn.Forestry;
using Timberborn.Localization; // Required for ILoc
using Timberborn.NaturalResources;
using Timberborn.Planting;
using Timberborn.PlantingUI;
using Timberborn.SelectionToolSystem;
using Timberborn.TemplateSystem;
using Timberborn.ToolSystem;
using Timberborn.ToolSystemUI;
using UnityEngine;

namespace Calloatti.NaturalResourcesTweaks
{
  public class MixedForestTool : ITool, IToolDescriptor
  {
    private readonly SelectionToolProcessor _selectionToolProcessor;
    private readonly PlantingSelectionService _plantingSelectionService;
    private readonly TemplateService _templateService;
    private readonly DevModePlantableSpawner _devModePlantableSpawner;
    private readonly ILoc _loc; // This was null!

    private PlantableSpec[] _availableTreesArray;
    private const float UsedTilesPercentage = 0.50f;

    public MixedForestTool(
        SelectionToolProcessorFactory selectionToolProcessorFactory,
        PlantingSelectionService plantingSelectionService,
        TemplateService templateService,
        DevModePlantableSpawner devModePlantableSpawner,
        ILoc loc) // 1. Added ILoc here
    {
      _plantingSelectionService = plantingSelectionService;
      _templateService = templateService;
      _devModePlantableSpawner = devModePlantableSpawner;
      _loc = loc; // 2. Assigned it here so DescribeTool can use it

      _selectionToolProcessor = selectionToolProcessorFactory.Create(PreviewCallback, ActionCallback, ShowNoneCallback, "PlantingCursor");
    }

    public void Enter()
    {
      if (_availableTreesArray == null)
      {
        _availableTreesArray = _templateService.GetAll<PlantableSpec>()
            .Where(p => p.HasSpec<TreeComponentSpec>() &&
                        p.GetSpec<NaturalResourceSpec>().UsableWithCurrentFeatureToggles &&
                        !p.TemplateName.Contains("Mangrove"))
            .OrderBy(t => t.PlantTimeInHours)
            .ToArray();
      }
      _selectionToolProcessor.Enter();
    }

    public void Exit() => _selectionToolProcessor.Exit();

    public ToolDescription DescribeTool()
    {
      // This will no longer crash because _loc is now assigned.
      return new ToolDescription.Builder(_loc.T("Calloatti.NaturalResourcesTweaks.ForestTool.Title"))
          .AddSection(_loc.T("Calloatti.NaturalResourcesTweaks.ForestTool.Description"))
          .Build();
    }

    private void PreviewCallback(IEnumerable<Vector3Int> inputBlocks, Ray ray) => ProcessArea(inputBlocks, ray, isAction: false);
    private void ActionCallback(IEnumerable<Vector3Int> inputBlocks, Ray ray) => ProcessArea(inputBlocks, ray, isAction: true);
    private void ShowNoneCallback() => _plantingSelectionService.UnhighlightAll();

    private void ProcessArea(IEnumerable<Vector3Int> inputBlocks, Ray ray, bool isAction)
    {
      if (_availableTreesArray == null || _availableTreesArray.Length == 0) return;

      var blocksList = inputBlocks.ToList();
      if (blocksList.Count == 0) return;

      // 1. CALCULATE SELECTION BOUNDS
      int minX = int.MaxValue, maxX = int.MinValue;
      int minY = int.MaxValue, maxY = int.MinValue;
      foreach (var b in blocksList)
      {
        if (b.x < minX) minX = b.x;
        if (b.x > maxX) maxX = b.x;
        if (b.y < minY) minY = b.y;
        if (b.y > maxY) maxY = b.y;
      }

      // 2. DEFINE ELLIPSE PARAMETERS
      float centerX = (minX + maxX) / 2f;
      float centerY = (minY + maxY) / 2f;
      float radX = (maxX - minX + 1) / 2f;
      float radY = (maxY - minY + 1) / 2f;

      var treeBuckets = new Dictionary<string, List<Vector3Int>>();
      foreach (var tree in _availableTreesArray)
      {
        treeBuckets[tree.TemplateName] = new List<Vector3Int>();
      }

      int treeCount = _availableTreesArray.Length;
      float bucketSize = 1.0f / treeCount;

      foreach (var block in blocksList)
      {
        // 3. APPLY ELLIPSE MASK
        float dx = (block.x - centerX) / radX;
        float dy = (block.y - centerY) / radY;
        if ((dx * dx + dy * dy) > 1.0f) continue;

        System.Random rnd = new System.Random(block.GetHashCode());

        // 4. DENSITY CHECK (50%)
        if (rnd.NextDouble() < UsedTilesPercentage) continue;

        // 5. RANDOM SELECTION LOGIC
        double treeRoll = rnd.NextDouble();
        int selectedTreeIndex = (int)(treeRoll / bucketSize);

        if (selectedTreeIndex < 0) selectedTreeIndex = 0;
        if (selectedTreeIndex >= treeCount) selectedTreeIndex = treeCount - 1;

        string speciesTemplate = _availableTreesArray[selectedTreeIndex].TemplateName;
        treeBuckets[speciesTemplate].Add(block);
      }

      foreach (var kvp in treeBuckets)
      {
        if (kvp.Value.Count > 0)
        {
          if (isAction)
          {
            _plantingSelectionService.MarkArea(kvp.Value, ray, kvp.Key);
            _devModePlantableSpawner.SpawnPlantables(kvp.Value, kvp.Key);
          }
          else
          {
            _plantingSelectionService.HighlightMarkableArea(kvp.Value, ray, kvp.Key);
          }
        }
      }
    }
  }
}