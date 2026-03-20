using Bindito.Core;

namespace Calloatti.NaturalResourcesTweaks
{
  [Context("Game")]
  internal class PatchConfigurator : IConfigurator
  {
    public void Configure(IContainerDefinition containerDefinition)
    {
      // Register our new custom tool and the UI adder so the game knows they exist
      containerDefinition.Bind<MixedForestTool>().AsSingleton();
      containerDefinition.Bind<MixedForestButtonAdder>().AsSingleton();
    }
  }
}