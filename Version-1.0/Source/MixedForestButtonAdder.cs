using System.Linq;
using Timberborn.PlantingUI;
using Timberborn.SingletonSystem;
using Timberborn.ToolButtonSystem;
using Timberborn.ToolSystem;

namespace Calloatti.NaturalResourcesTweaks
{
  public class MixedForestButtonAdder : IPostLoadableSingleton
  {
    private readonly ToolButtonFactory _toolButtonFactory;
    private readonly ToolGroupService _toolGroupService;
    private readonly ToolButtonService _toolButtonService;
    private readonly MixedForestTool _mixedForestTool;

    public MixedForestButtonAdder(
        ToolButtonFactory toolButtonFactory,
        ToolGroupService toolGroupService,
        ToolButtonService toolButtonService,
        MixedForestTool mixedForestTool)
    {
      _toolButtonFactory = toolButtonFactory;
      _toolGroupService = toolGroupService;
      _toolButtonService = toolButtonService;
      _mixedForestTool = mixedForestTool; // Injected via DI
    }

    public void PostLoad()
    {
      // 1. Get the vanilla Forestry group
      ToolGroupSpec forestryGroup = _toolGroupService.GetGroup("Forestry");

      // 2. Find an existing vanilla tool in the Forestry group so we can trace it back to the UI wrapper
      ToolButton existingForestryButton = _toolButtonService.ToolButtons
          .FirstOrDefault(b => _toolGroupService.IsAssignedToGroup(b.Tool, forestryGroup));

      if (existingForestryButton != null)
      {
        ToolGroupButton groupButton = _toolButtonService.GetToolGroupButton(existingForestryButton);

        // 3. Create our UI button. The game will look for your icon key via its asset loader.
        ToolButton myButton = _toolButtonFactory.Create(_mixedForestTool, "foresticon", groupButton.ToolButtonsElement);

        // --- CRITICAL WIRING STEP ---
        // This registers the click handler so the button actually activates the tool.
        myButton.PostLoad();

        // 4. Find the Cancel button so we can cleanly place our new tool immediately before it
        ToolButton cancelBtn = _toolButtonService.ToolButtons
            .FirstOrDefault(b => b.Tool is CancelPlantingTool && _toolGroupService.IsAssignedToGroup(b.Tool, forestryGroup));

        if (cancelBtn != null)
        {
          myButton.Root.PlaceBehind(cancelBtn.Root);
        }

        // 5. Register the button and tool with the game's native systems
        groupButton.AddTool(myButton);
        _toolGroupService.AssignToGroup(forestryGroup, _mixedForestTool);
      }
    }
  }
}