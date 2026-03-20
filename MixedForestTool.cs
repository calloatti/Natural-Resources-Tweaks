using System;
using System.Collections.Generic;
using System.Linq;
using Calloatti.Config;
using Timberborn.BlockSystem;
using Timberborn.Forestry;
using Timberborn.Localization;
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
  public partial class MixedForestTool : ITool, IToolDescriptor
  {
    private readonly SelectionToolProcessor _selectionToolProcessor;
    private readonly PlantingSelectionService _plantingSelectionService;
    private readonly TemplateService _templateService;
    private readonly DevModePlantableSpawner _devModePlantableSpawner;
    private readonly ILoc _loc;

    private PlantableSpec[] _availableTreesArray;
    private SimpleIniConfig _config;
    private string _forestMethod;

    public MixedForestTool(
        SelectionToolProcessorFactory selectionToolProcessorFactory,
        PlantingSelectionService plantingSelectionService,
        TemplateService templateService,
        DevModePlantableSpawner devModePlantableSpawner,
        ILoc loc)
    {
      _plantingSelectionService = plantingSelectionService;
      _templateService = templateService;
      _devModePlantableSpawner = devModePlantableSpawner;
      _loc = loc;

      _selectionToolProcessor = selectionToolProcessorFactory.Create(PreviewCallback, ActionCallback, ShowNoneCallback, "PlantingCursor");
    }

    public void Enter()
    {
      if (_config == null)
      {
        _config = new SimpleIniConfig("NaturalResourcesTweaks.txt");
        _forestMethod = _config.GetString("ForestMethod", "SimpleForest");

        // Load parameters for each method
        LoadSimpleForestConfig(_config);

        _config.Save();
      }

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
      return new ToolDescription.Builder(_loc.T("Calloatti.NaturalResourcesTweaks.ForestTool.Title"))
          .AddSection(_loc.T("Calloatti.NaturalResourcesTweaks.ForestTool.Description"))
          .Build();
    }

    private void PreviewCallback(IEnumerable<Vector3Int> inputBlocks, Ray ray)
    {
      if (_forestMethod == "SimpleForest") SimpleForest(inputBlocks, ray, isAction: false);
    }

    private void ActionCallback(IEnumerable<Vector3Int> inputBlocks, Ray ray)
    {
      if (_forestMethod == "SimpleForest") SimpleForest(inputBlocks, ray, isAction: true);
    }

    private void ShowNoneCallback() => _plantingSelectionService.UnhighlightAll();
  }
}