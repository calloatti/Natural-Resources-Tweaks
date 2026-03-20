using System;
using System.Collections.Generic;
using System.Linq;
using Calloatti.Config;
using UnityEngine;

namespace Calloatti.NaturalResourcesTweaks
{
  public partial class MixedForestTool
  {
    private float _simpleForestUsedTilesPercentage;

    private void LoadSimpleForestConfig(SimpleIniConfig config)
    {
      _simpleForestUsedTilesPercentage = config.GetFloat("SimpleForest_UsedTilesPercentage", 0.50f);
    }

    private void SimpleForest(IEnumerable<Vector3Int> inputBlocks, Ray ray, bool isAction)
    {
      if (_availableTreesArray == null || _availableTreesArray.Length == 0) return;

      var blocksList = inputBlocks.ToList();
      if (blocksList.Count == 0) return;

      int minX = int.MaxValue, maxX = int.MinValue;
      int minY = int.MaxValue, maxY = int.MinValue;
      foreach (var b in blocksList)
      {
        if (b.x < minX) minX = b.x;
        if (b.x > maxX) maxX = b.x;
        if (b.y < minY) minY = b.y;
        if (b.y > maxY) maxY = b.y;
      }

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
        float dx = (block.x - centerX) / radX;
        float dy = (block.y - centerY) / radY;
        if ((dx * dx + dy * dy) > 1.0f) continue;

        System.Random rnd = new System.Random(block.GetHashCode());
        if (rnd.NextDouble() < _simpleForestUsedTilesPercentage) continue;

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