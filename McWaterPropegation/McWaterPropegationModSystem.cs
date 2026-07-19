using Vintagestory.API.Common;

namespace McWaterPropegation;

public partial class McWaterPropegationModSystem : ModSystem
{

    public override void StartPre(ICoreAPI api)
    {
        base.StartPre(api);
        AutoSetup(api);
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        base.AssetsLoaded(api);
        AutoAssetsLoaded(api);
    }

    public override void Dispose()
    {
        base.Dispose();
        AutoDispose();
    }

}
