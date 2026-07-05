using HumanFortress.App.States;

namespace HumanFortress.App.GameStates;

internal interface IFortressPlayRuntimeHost
{
    FortressStateRuntimePorts CreateRuntimePorts();

    void InitializeWorld(int sizeInChunks, int maxZ);
}
