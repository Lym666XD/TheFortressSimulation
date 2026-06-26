using HumanFortress.App.Runtime;

namespace HumanFortress.App.GameStates;

internal interface IFortressPlayRuntimeHost
{
    IFortressRuntimeSessionAccess CreateRuntimeAccess();

    void InitializeWorld(int sizeInChunks, int maxZ);
}
