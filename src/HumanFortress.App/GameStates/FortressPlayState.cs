using System;
using HumanFortress.App.States;
using HumanFortress.Core.Time;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.World;
using SadConsole;

namespace HumanFortress.App.GameStates
{
    /// <summary>
    /// Fortress play state with simulation loop per UPDATE_ORDER.md.
    /// </summary>
    public class FortressPlayState : GameState
    {
        private FortressState? _fortressState;
        private TickScheduler? _tickScheduler;
        private CommandQueue? _commandQueue;
        private DiffLog? _diffLog;
        private ChunkLifecycleManager? _lifecycleManager;
        private World? _world;
        private bool _isSimulationRunning;
        private double _accumulator;
        private const double TICK_RATE = 1.0 / 50.0; // 50 TPS
        
        public override GameStateType Type => GameStateType.FortressPlay;
        
        public override void Enter()
        {
            try
            {
                System.Console.WriteLine("[FortressPlayState] Enter() called");
                System.Console.WriteLine($"[FortressPlayState] EmbarkLocation: {FortressState.EmbarkLocation}");
                System.Console.WriteLine($"[FortressPlayState] FortressSize: {FortressState.FortressSize}");

                // Create fortress visualization state
                System.Console.WriteLine("[FortressPlayState] Creating FortressState");
                _fortressState = new FortressState();
                _fortressState.IsFocused = true;
                System.Console.WriteLine("[FortressPlayState] Setting GameHost.Instance.Screen");
                GameHost.Instance.Screen = _fortressState;
                GameHost.Instance.Screen.IsFocused = true;
                System.Console.WriteLine("[FortressPlayState] FortressState created and set as screen");

                // Initialize simulation components
                System.Console.WriteLine("[FortressPlayState] Initializing simulation");
                InitializeSimulation();

                // Start simulation
                _isSimulationRunning = true;
                System.Console.WriteLine("[FortressPlayState] Simulation started");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[FortressPlayState] ERROR in Enter(): {ex.Message}");
                System.Console.WriteLine($"[FortressPlayState] Stack trace: {ex.StackTrace}");
                throw;
            }
        }
        
        private void InitializeSimulation()
        {
            try
            {
                System.Console.WriteLine("[InitializeSimulation] Starting initialization");

                // Get world from fortress state (created during generation)
                var worldField = typeof(FortressState).GetField("_world",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                System.Console.WriteLine($"[InitializeSimulation] Got world field: {worldField != null}");

                _world = worldField?.GetValue(_fortressState) as World;
                System.Console.WriteLine($"[InitializeSimulation] World retrieved: {_world != null}");

                if (_world == null)
                {
                    System.Console.WriteLine("[InitializeSimulation] WARNING: No world available for simulation");
                    return;
                }

                System.Console.WriteLine($"[InitializeSimulation] World size: {_world.SizeInChunks}x{_world.SizeInChunks} chunks");

                // Initialize core simulation components
                System.Console.WriteLine("[InitializeSimulation] Creating simulation components");
                _tickScheduler = new TickScheduler();
                _commandQueue = new CommandQueue();
                _diffLog = new DiffLog();
                _lifecycleManager = new ChunkLifecycleManager(_world);

                // Register simulation systems
                RegisterSimulationSystems();

                System.Console.WriteLine("[InitializeSimulation] Simulation initialized with 50 TPS target");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[InitializeSimulation] ERROR: {ex.Message}");
                System.Console.WriteLine($"[InitializeSimulation] Stack trace: {ex.StackTrace}");
            }
        }
        
        private void RegisterSimulationSystems()
        {
            // Register systems in UPDATE_ORDER
            // For Phase C, we just need the basic loop running
            // Later phases will add actual systems
        }
        
        public override void Update(double deltaTime)
        {
            if (!_isSimulationRunning || _tickScheduler == null || _world == null)
                return;
            
            // Fixed timestep accumulator per UPDATE_ORDER.md
            _accumulator += deltaTime;
            
            int ticksProcessed = 0;
            const int MAX_TICKS_PER_FRAME = 3; // Prevent spiral of death
            
            while (_accumulator >= TICK_RATE && ticksProcessed < MAX_TICKS_PER_FRAME)
            {
                // Execute one simulation tick
                ExecuteSimulationTick();
                
                _accumulator -= TICK_RATE;
                ticksProcessed++;
            }
            
            // Cap accumulator to prevent excessive catch-up
            if (_accumulator > TICK_RATE * MAX_TICKS_PER_FRAME)
            {
                _accumulator = TICK_RATE;
            }
        }
        
        private void ExecuteSimulationTick()
        {
            // Use the tick scheduler's built-in single tick execution
            // This handles the UPDATE_ORDER internally
            _tickScheduler!.ExecuteSingleTick();

            var tick = _tickScheduler.CurrentTick;

            // Update chunk LOD levels based on camera
            UpdateChunkLOD(tick);
        }

        private void UpdateChunkLOD(ulong tick)
        {
            if (_lifecycleManager != null && _fortressState != null)
            {
                var cameraField = typeof(FortressState).GetField("_cameraPos",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var currentZField = typeof(FortressState).GetField("_currentZ",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (cameraField != null && currentZField != null)
                {
                    var cameraPos = (SadRogue.Primitives.Point)cameraField.GetValue(_fortressState);
                    var currentZ = (int)currentZField.GetValue(_fortressState);

                    _lifecycleManager.UpdateLODLevels(cameraPos.X, cameraPos.Y, currentZ, tick);
                }
            }
        }
        
        
        public override void Exit()
        {
            _isSimulationRunning = false;
            System.Console.WriteLine("Exited Fortress Play State");
        }
        
        public override void HandleInput()
        {
            // Input is handled by FortressState (SadConsole ScreenObject)
            // Commands from UI are queued to _commandQueue
        }
    }
}