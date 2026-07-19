using System.Text.Json;

namespace HumanFortress.Jobs.Configuration;

internal sealed class SchedulerTunings
{
    internal int Threads { get; init; } = 1; // v1 default
    internal string QueuePolicy { get; init; } = "single"; // v2: work_stealing

    internal Budget Hauling { get; init; } = new(128);
    internal Budget Mining { get; init; } = new(128);
    internal Budget Construction { get; init; } = new(256);

    internal bool PerJobStatsLogging { get; init; } = true;
    internal string LogLevel { get; init; } = "info";
    internal int BackpressureMaxCarryoverTicks { get; init; } = 8;
    internal bool DebugPanel { get; init; }
    internal HaulingLimitSettings HaulingLimits { get; init; } = new();
    internal WorkerSelectionStrategy WorkerSelection { get; init; } = WorkerSelectionStrategy.Closest;

    internal readonly record struct Budget(int PlanPerTick);

    internal sealed class HaulingLimitSettings
    {
        internal int MaxActive { get; init; }
        internal int ReserveForMining { get; init; }
        internal int ReserveBacklogThreshold { get; init; } = 1;
        internal int BacklogIntakeCap { get; init; }
        internal int BacklogIntakeThreshold { get; init; } = 1;
    }

    internal static SchedulerTunings LoadFromJson(string? json, string source, Action<string>? log = null)
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
                    ReadPositiveInt32(hEl, "plan_per_tick", h.PlanPerTick));
            }

            if (budgets.TryGetProperty("mining", out var mEl))
            {
                m = new Budget(
                    ReadPositiveInt32(mEl, "plan_per_tick", m.PlanPerTick));
            }

            if (budgets.TryGetProperty("construction", out var cEl))
            {
                c = new Budget(
                    ReadPositiveInt32(cEl, "plan_per_tick", c.PlanPerTick));
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

    private static int ReadPositiveInt32(JsonElement element, string propertyName, int fallback)
    {
        if (!element.TryGetProperty(propertyName, out var property) || !property.TryGetInt32(out var value))
            return fallback;

        return Math.Max(1, value);
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
