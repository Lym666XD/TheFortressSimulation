using System.Diagnostics;
using System.Reflection;
using HumanFortress.Contracts.Runtime.Checkpoints;
using HumanFortress.Contracts.Time;
using HumanFortress.Core.Time;
using HumanFortress.Runtime;
using HumanFortress.Runtime.Composition;
using HumanFortress.Runtime.Session;
using SadRogue.Primitives;

internal static class TickSchedulerLifecycleRegressionTests
{
    internal static void RunAll()
    {
        TestBlockedSystemReturnsTimedOutPositionAndCanFinishLater();
        TestTickThreadCanRequestItsOwnStop();
        TestStopIsIdempotentAndSchedulerCanRestartAndReset();
        TestPausedWaitWakesForStop();
        TestTimedOutRuntimeGenerationIsReplacedAndCannotPublishLateWork();
        TestRuntimeDisposeIsIdempotentAndRejectsRestart();
    }

    private static void TestRuntimeDisposeIsIdempotentAndRejectsRestart()
    {
        var core = new FortressRuntimeSessionCore(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        var lifecycle = (IFortressRuntimeSessionLifecyclePort)core;
        var checkpoints = (IFortressRuntimeSessionCheckpointPort)core;
        var lifecycleOwner = GetPrivateField<RuntimeSessionLifecycleOwner>(core, "_lifecycle");

        lifecycle.InitializeWorld(sizeInChunks: 2, maxZ: 2);
        lifecycle.StartFortressPlay(enqueueAutoDig: false);
        RegressionAssert.True(
            SpinWait.SpinUntil(
                () => lifecycleOwner.Services.TickScheduler.CurrentTick > 0,
                millisecondsTimeout: 2000),
            "Runtime disposal test did not advance the scheduler.");

        ((IDisposable)core).Dispose();
        ((IDisposable)core).Dispose();
        bool restartRejected = false;
        try
        {
            lifecycle.StartFortressPlay(enqueueAutoDig: false);
        }
        catch (ObjectDisposedException)
        {
            restartRejected = true;
        }

        RegressionAssert.True(
            lifecycleOwner.ActiveSession == null
            && !lifecycleOwner.Services.TickScheduler.HasActiveThread
            && !checkpoints.TryGetLatestCheckpointIdentity(out _)
            && restartRejected,
            "Runtime disposal was not idempotent or left scheduler/checkpoint authority active.");
    }

    private static void TestBlockedSystemReturnsTimedOutPositionAndCanFinishLater()
    {
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        var scheduler = new TickScheduler();
        scheduler.RegisterSystem(new BlockingReadTickSystem(entered, release));

        TickSchedulerStopResult timedOut = default;
        TickSchedulerStopResult completed = default;
        try
        {
            scheduler.Start();
            RegressionAssert.True(
                entered.Wait(millisecondsTimeout: 1000),
                "TickScheduler blocking-system test did not enter ReadTick.");

            timedOut = scheduler.TryStop(TimeSpan.FromMilliseconds(25));
            bool resetRejected = false;
            try
            {
                scheduler.ResetForNewSession();
            }
            catch (InvalidOperationException)
            {
                resetRejected = true;
            }

            RegressionAssert.True(
                timedOut.Status == TickSchedulerStopStatus.TimedOut
                && timedOut.Tick == 0
                && timedOut.Phase == TickSchedulerExecutionPhase.Read
                && timedOut.SystemId == BlockingReadTickSystem.Id
                && resetRejected
                && !scheduler.IsRunning
                && scheduler.HasActiveThread,
                "TickScheduler did not return the blocked execution position or allowed reuse after bounded stop timeout.");

            release.Set();
            completed = scheduler.TryStop(TimeSpan.FromSeconds(1));
        }
        finally
        {
            release.Set();
            _ = scheduler.TryStop(TimeSpan.FromSeconds(1));
        }

        RegressionAssert.True(
            (completed.Status is TickSchedulerStopStatus.Stopped
                or TickSchedulerStopStatus.AlreadyStopped)
            && !scheduler.HasActiveThread,
            "TickScheduler did not finish stopping after the blocked system was released.");
    }

    private static void TestTimedOutRuntimeGenerationIsReplacedAndCannotPublishLateWork()
    {
        using var oldPostTickEntered = new ManualResetEventSlim(false);
        using var releaseOldPostTick = new ManualResetEventSlim(false);
        var core = new FortressRuntimeSessionCore(
            AppContext.BaseDirectory,
            strictContent: false,
            contentWarningsAsErrors: false);
        var lifecycle = (IFortressRuntimeSessionLifecyclePort)core;
        var bootstrap = (IFortressRuntimeSessionBootstrapPort)core;
        var checkpoints = (IFortressRuntimeSessionCheckpointPort)core;
        var lifecycleOwner = GetPrivateField<RuntimeSessionLifecycleOwner>(core, "_lifecycle");

        RuntimeSessionServices? oldServices = null;
        try
        {
            lifecycle.InitializeWorld(sizeInChunks: 2, maxZ: 2);
            var oldSession = lifecycleOwner.ActiveSession
                ?? throw new InvalidOperationException("Missing active runtime session in lifecycle test.");
            oldServices = lifecycleOwner.Services;
            var oldNotifier = lifecycleOwner.WorkshopCompletionNotifier;
            oldServices.TickScheduler.PostTick += _ =>
            {
                oldPostTickEntered.Set();
                releaseOldPostTick.Wait();
            };

            lifecycle.StartFortressPlay(enqueueAutoDig: false);
            RegressionAssert.True(
                oldPostTickEntered.Wait(millisecondsTimeout: 2000),
                "Runtime lifecycle test did not block the old generation at PostTick.");

            var timedOut = lifecycle.Stop(TimeSpan.FromMilliseconds(25));
            var isolatedServices = lifecycleOwner.Services;
            var isolatedNotifier = lifecycleOwner.WorkshopCompletionNotifier;
            var oldHostCore = GetPrivateFieldValue(oldSession.Host, "_core")
                ?? throw new InvalidOperationException("Missing runtime host core in lifecycle test.");
            bool oldPipelineRetainedAfterTimeout = GetPrivateFieldValue(oldHostCore, "_pipeline") != null;

            lifecycle.InitializeWorld(sizeInChunks: 2, maxZ: 2);
            var replacementServices = lifecycleOwner.Services;
            var replacementNotifier = lifecycleOwner.WorkshopCompletionNotifier;
            int replacementNotifications = 0;
            bootstrap.SetWorkshopCompletionHandler(_ => replacementNotifications++);

            oldNotifier.Notify(0, 0, 0, new Rectangle(0, 0, 1, 1), "old", 0);
            replacementNotifier.Notify(0, 0, 0, new Rectangle(0, 0, 1, 1), "new", 0);

            releaseOldPostTick.Set();
            var oldCompleted = oldSession.Host.Stop(TimeSpan.FromSeconds(1));
            bool oldPipelineDetachedAfterStop = GetPrivateFieldValue(oldHostCore, "_pipeline") == null;
            bool oldPublishedAfterReplacement = checkpoints.TryGetLatestCheckpointIdentity(out _);

            lifecycle.StartFortressPlay(enqueueAutoDig: false);
            RegressionAssert.True(
                SpinWait.SpinUntil(
                    () => checkpoints.TryGetLatestCheckpointIdentity(out _),
                    millisecondsTimeout: 2000),
                "Replacement runtime did not publish a committed checkpoint.");
            var replacementStop = lifecycle.Stop(TimeSpan.FromSeconds(1));
            bool hasReplacementCheckpoint = checkpoints.TryGetLatestCheckpointIdentity(
                out RuntimeCheckpointIdentityData replacementCheckpoint);

            RegressionAssert.True(
                timedOut.Status == TickSchedulerStopStatus.TimedOut
                && timedOut.Phase == TickSchedulerExecutionPhase.PostTick
                && !oldServices.TickScheduler.HasActiveThread
                && oldCompleted.HasStopped
                && !ReferenceEquals(oldServices, isolatedServices)
                && !ReferenceEquals(isolatedServices, replacementServices)
                && !ReferenceEquals(oldNotifier, isolatedNotifier)
                && !ReferenceEquals(isolatedNotifier, replacementNotifier)
                && oldPipelineRetainedAfterTimeout
                && oldPipelineDetachedAfterStop
                && replacementNotifications == 1
                && !oldPublishedAfterReplacement
                && replacementStop.HasStopped
                && replacementServices.TickScheduler.CurrentTick > 0
                && hasReplacementCheckpoint
                && replacementCheckpoint.SessionGeneration > 0,
                "A timed-out runtime generation was reused, published late work, or blocked its replacement.");
        }
        finally
        {
            releaseOldPostTick.Set();
            if (oldServices != null)
                _ = oldServices.TickScheduler.TryStop(TimeSpan.FromSeconds(1));
            _ = lifecycle.Stop(TimeSpan.FromSeconds(1));
        }
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
        where T : class
    {
        return (T)(GetPrivateFieldValue(instance, fieldName)
            ?? throw new InvalidOperationException($"Missing lifecycle test field '{fieldName}'."));
    }

    private static object? GetPrivateFieldValue(object instance, string fieldName)
    {
        return instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance);
    }

    private static void TestTickThreadCanRequestItsOwnStop()
    {
        using var stopObserved = new ManualResetEventSlim(false);
        var scheduler = new TickScheduler();
        var system = new SelfStoppingTickSystem(scheduler, stopObserved);
        scheduler.RegisterSystem(system);

        scheduler.Start();
        RegressionAssert.True(
            stopObserved.Wait(millisecondsTimeout: 1000),
            "TickScheduler self-stop test did not execute the tick system.");
        RegressionAssert.True(
            SpinWait.SpinUntil(
                () => scheduler.ExecutionPosition.Phase == TickSchedulerExecutionPhase.Stopped,
                millisecondsTimeout: 1000),
            "TickScheduler did not terminate after a tick-thread stop request.");

        var finalStop = scheduler.TryStop(TimeSpan.FromMilliseconds(100));
        RegressionAssert.True(
            system.StopResult.Status == TickSchedulerStopStatus.SelfStopRequested
            && finalStop.Status == TickSchedulerStopStatus.AlreadyStopped,
            "TickScheduler tried to join its own tick thread or did not report the completed self-stop idempotently.");
    }

    private static void TestStopIsIdempotentAndSchedulerCanRestartAndReset()
    {
        var neverStarted = new TickScheduler();
        RegressionAssert.True(
            neverStarted.TryStop(TimeSpan.Zero).Status == TickSchedulerStopStatus.AlreadyStopped,
            "TickScheduler did not report an unstarted scheduler as already stopped.");

        var scheduler = new TickScheduler();
        scheduler.RegisterSystem(new CountingTickSystem());
        try
        {
            scheduler.Start();
            RegressionAssert.True(
                SpinWait.SpinUntil(() => scheduler.CurrentTick > 0, millisecondsTimeout: 1000),
                "TickScheduler restart test did not advance its first run.");

            var firstStop = scheduler.TryStop(TimeSpan.FromSeconds(1));
            var repeatedStop = scheduler.TryStop(TimeSpan.Zero);
            ulong firstRunTick = scheduler.CurrentTick;

            scheduler.Start();
            RegressionAssert.True(
                SpinWait.SpinUntil(() => scheduler.CurrentTick > firstRunTick, millisecondsTimeout: 1000),
                "TickScheduler did not restart after a completed stop.");
            var secondStop = scheduler.TryStop(TimeSpan.FromSeconds(1));

            scheduler.ResetForNewSession();
            scheduler.ExecuteSingleTick();

            RegressionAssert.True(
                firstStop.Status == TickSchedulerStopStatus.Stopped
                && repeatedStop.Status == TickSchedulerStopStatus.AlreadyStopped
                && secondStop.Status == TickSchedulerStopStatus.Stopped
                && scheduler.CurrentTick == 1
                && scheduler.ExecutionPosition.Phase == TickSchedulerExecutionPhase.Stopped,
                "TickScheduler stop, restart, reset, or repeated-stop lifecycle state was inconsistent.");
        }
        finally
        {
            _ = scheduler.TryStop(TimeSpan.FromSeconds(1));
        }
    }

    private static void TestPausedWaitWakesForStop()
    {
        var scheduler = new TickScheduler();
        scheduler.Pause();
        try
        {
            scheduler.Start();
            RegressionAssert.True(
                SpinWait.SpinUntil(
                    () => scheduler.ExecutionPosition.Phase == TickSchedulerExecutionPhase.Paused,
                    millisecondsTimeout: 1000),
                "TickScheduler did not enter its paused wait state.");

            var stopwatch = Stopwatch.StartNew();
            var result = scheduler.TryStop(TimeSpan.FromSeconds(1));
            stopwatch.Stop();

            RegressionAssert.True(
                result.Status == TickSchedulerStopStatus.Stopped
                && stopwatch.Elapsed < TimeSpan.FromMilliseconds(250),
                "TickScheduler stop did not wake the paused tick loop promptly.");
        }
        finally
        {
            _ = scheduler.TryStop(TimeSpan.FromSeconds(1));
        }
    }

    private sealed class BlockingReadTickSystem : ITick
    {
        internal const string Id = "Lifecycle.BlockingRead";

        private readonly ManualResetEventSlim _entered;
        private readonly ManualResetEventSlim _release;

        internal BlockingReadTickSystem(
            ManualResetEventSlim entered,
            ManualResetEventSlim release)
        {
            _entered = entered;
            _release = release;
        }

        public int Priority => 1;

        public string SystemId => Id;

        public void ReadTick(ulong tick)
        {
            _entered.Set();
            _release.Wait();
        }

        public void WriteTick(ulong tick)
        {
        }
    }

    private sealed class SelfStoppingTickSystem : ITick
    {
        private readonly TickScheduler _scheduler;
        private readonly ManualResetEventSlim _stopObserved;

        internal SelfStoppingTickSystem(
            TickScheduler scheduler,
            ManualResetEventSlim stopObserved)
        {
            _scheduler = scheduler;
            _stopObserved = stopObserved;
        }

        internal TickSchedulerStopResult StopResult { get; private set; }

        public int Priority => 1;

        public string SystemId => "Lifecycle.SelfStop";

        public void ReadTick(ulong tick)
        {
            StopResult = _scheduler.TryStop(TimeSpan.FromMilliseconds(25));
            _stopObserved.Set();
        }

        public void WriteTick(ulong tick)
        {
        }
    }

    private sealed class CountingTickSystem : ITick
    {
        public int Priority => 1;

        public string SystemId => "Lifecycle.Counting";

        public void ReadTick(ulong tick)
        {
        }

        public void WriteTick(ulong tick)
        {
        }
    }
}
