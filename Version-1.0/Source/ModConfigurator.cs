using Bindito.Core;

namespace Calloatti.NaturalResourcesTweaks
{
  [Context("Game")]
  internal class ModConfigurator : Configurator
  {
    protected override void Configure()
    {
      // 1. Mixed Forest Tool
      Bind<MixedForestTool>().AsSingleton();
      Bind<MixedForestButtonAdder>().AsSingleton();

    }
  }
}