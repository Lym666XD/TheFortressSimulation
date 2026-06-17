using System.Text.Json;

namespace HumanFortress.App.Jobs;

public sealed class SchedulerTunings
{
    public int Threads { get; init; } = 1; // v1 default
    public string QueuePolicy { get; init; } = "single"; // v2: work_stealing

    public Budget Hauling { get; init; } = new(128, 2);
    public Budget Mining { get; init; } = new(128, 2);
    public Budget Construction { get; init; } = new(256, 3);

    public bool PerJobStatsLogging { get; init; } = true;
    public string LogLevel { get; init; } = "info";
    public int BackpressureMaxCarryoverTicks { get; init; } = 8;
    public bool DebugPanel { get; init; }
    public HaulingLimitSettings HaulingLimits { get; init; } = new();
    public WorkerSelectionStrategy WorkerSelection { get; init; } = WorkerSelectionStrategy.Closest;

    public readonly record struct Budget(int PlanPerTick, int Ms);

    public sealed class HaulingLimitSettings
    {
        public int MaxActive { get; init; }
        public int ReserveForMining { get; init; }
        public int ReserveBacklogThreshold { get; init; } = 1;
        public int BacklogIntakeCap { get; init; }
        public int BacklogIntakeThreshold { get; init; } = 1;
    }

    public static SchedulerTunings LoadFromJson(string? json, string source, Action<string>? log = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new SchedulerTunings();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return LoadFromJsonElement(doc.RootElement);
        }
        catch (Exception ex)
        {
            log?.Invoke($"[SCHED.TUNINGS] Failed to load tunings from {source}: {ex.Message}");
            return new SchedulerTunings();
        }
    }

    private static SchedulerTunings LoadFromJsonElement(JsonElement root)
    {
        var t = new SchedulerTunings();

        int threads = root.TryGetProperty("threads", out var threadsEl) ? threadsEl.GetInt32() : t.Threads;
        string queue = root.TryGetProperty("queue_policy", out var qEl) ? (qEl.GetString() ?? t.QueuePolicy) : t.QueuePolicy;

        var budgets = root.TryGetProperty("budgets", out var bEl) ? bEl : default;
        var h = t.Hauling;
        var m = t.Mining;
        var c = t.Construction;
        if (budgets.ValueKind == JsonValueKind.Object)
        {
            if (budgets.TryGetProperty("hauling", out var hEl))
            {
                h = new Budget(
                    hEl.TryGetProperty("plan_per_tick", out var p) ? p.GetInt32() : h.PlanPerTick,
                    hEl.TryGetProperty("ms", out var ms) ? ms.GetInt32() : h.Ms);
            }

            if (budgets.TryGetProperty("mining", out var mEl))
            {
                m = new Budget(
                    mEl.TryGetProperty("plan_per_tick", out var p) ? p.GetInt32() : m.PlanPerTick,
                    mEl.TryGetProperty("ms", out var ms) ? ms.GetInt32() : m.Ms);
            }

            if (budgets.TryGetProperty("construction", out var cEl))
            {
                c = new Budget(
                    cEl.TryGetProperty("plan_per_tick", out var p) ? p.GetInt32() : c.PlanPerTick,
                    cEl.TryGetProperty("ms", out var ms) ? ms.GetInt32() : c.Ms);
            }
        }

        var logging = root.TryGetProperty("logging", out var lEl) ? lEl : default;
        bool perJob = logging.ValueKind == JsonValueKind.Object && logging.TryGetProperty("per_job_stats", out var pjs)
            ? pjs.GetBoolean()
            : t.PerJobStatsLogging;
        string level = logging.ValueKind == JsonValueKind.Object && logging.TryGetProperty("level", out var lvl)
            ? (lvl.GetString() ?? t.LogLevel)
            : t.LogLevel;
        bool debugPanel = logging.ValueKind == JsonValueKind.Object && logging.TryGetProperty("debug_panel", out var dp)
            ? dp.GetBoolean()
            : t.DebugPanel;

        var haulingLimits = t.HaulingLimits;
        if (root.TryGetProperty("hauling_limits", out var hlEl) && hlEl.ValueKind == JsonValueKind.Object)
        {
            int maxActive = hlEl.TryGetProperty("max_active", out var ma) ? ma.GetInt32() : haulingLimits.MaxActive;
            int reserve = hlEl.TryGetProperty("reserve_for_mining", out var rf) ? rf.GetInt32() : haulingLimits.ReserveForMining;
            int reserveThreshold = hlEl.TryGetProperty("reserve_backlog_threshold", out var rbt) ? rbt.GetInt32() : haulingLimits.ReserveBacklogThreshold;
            int backlogIntake = hlEl.TryGetProperty("backlog_intake_cap", out var bic) ? bic.GetInt32() : haulingLimits.BacklogIntakeCap;
            int backlogThreshold = hlEl.TryGetProperty("backlog_intake_threshold", out var bit) ? bit.GetInt32() : haulingLimits.BacklogIntakeThreshold;
            haulingLimits = new HaulingLimitSettings
            {
                MaxActive = Math.Max(0, maxActive),
                ReserveForMining = Math.Max(0, reserve),
                ReserveBacklogThreshold = Math.Max(0, reserveThreshold),
                BacklogIntakeCap = Math.Max(0, backlogIntake),
                BacklogIntakeThreshold = Math.Max(0, backlogThreshold)
            };
        }

        int carry = t.BackpressureMaxCarryoverTicks;
        if (root.TryGetProperty("backpressure", out var bpEl) && bpEl.ValueKind == JsonValueKind.Object)
        {
            if (bpEl.TryGetProperty("max_carryover_ticks", out var mc))
            {
                carry = Math.Max(1, mc.GetInt32());
            }
        }

        var workerSelection = t.WorkerSelection;
        if (root.TryGetProperty("worker_selection", out var wsEl))
        {
            if (wsEl.ValueKind == JsonValueKind.String)
            {
                workerSelection = ParseStrategy(wsEl.GetString(), workerSelection);
            }
            else if (wsEl.ValueKind == JsonValueKind.Object && wsEl.TryGetProperty("strategy", out var stratEl))
            {
                workerSelection = ParseStrategy(stratEl.GetString(), workerSelection);
            }
        }

        return new SchedulerTunings
        {
            Threads = Math.Max(1, threads),
            QueuePolicy = queue,
            Hauling = h,
            Mining = m,
            Construction = c,
            PerJobStatsLogging = perJob,
            LogLevel = level,
            BackpressureMaxCarryoverTicks = carry,
            DebugPanel = debugPanel,
            HaulingLimits = haulingLimits,
            WorkerSelection = workerSelection
        };
    }

    private static WorkerSelectionStrategy ParseStrategy(string? value, WorkerSelectionStrategy fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;

        return value.ToLowerInvariant() switch
        {
            "idle" or "idle_first" => WorkerSelectionStrategy.IdleFirst,
            "skill" or "highest_skill" => WorkerSelectionStrategy.HighestSkill,
            _ => WorkerSelectionStrategy.Closest
        };
    }
}
