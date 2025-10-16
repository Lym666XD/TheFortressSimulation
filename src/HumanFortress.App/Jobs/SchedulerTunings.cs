using System;
using System.IO;
using System.Text.Json;

namespace HumanFortress.App.Jobs
{
    public sealed class SchedulerTunings
    {
        public int Threads { get; init; } = 1; // v1 default
        public string QueuePolicy { get; init; } = "single"; // v2: work_stealing

        public Budget Hauling { get; init; } = new Budget(128, 2);
        public Budget Mining { get; init; } = new Budget(128, 2);
        public Budget Construction { get; init; } = new Budget(256, 3);

        public bool PerJobStatsLogging { get; init; } = true;
        public string LogLevel { get; init; } = "info";
        public int BackpressureMaxCarryoverTicks { get; init; } = 8;

        public readonly record struct Budget(int PlanPerTick, int Ms);

        public static SchedulerTunings LoadFromContent(string baseDir)
        {
            try
            {
                var path = Path.Combine(baseDir, "content", "registries", "tuning.scheduler.json");
                if (!File.Exists(path)) return new SchedulerTunings();
                var json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var t = new SchedulerTunings();

                int threads = root.TryGetProperty("threads", out var threadsEl) ? threadsEl.GetInt32() : t.Threads;
                string queue = root.TryGetProperty("queue_policy", out var qEl) ? (qEl.GetString() ?? t.QueuePolicy) : t.QueuePolicy;

                var budgets = root.TryGetProperty("budgets", out var bEl) ? bEl : default;
                var h = t.Hauling; var m = t.Mining; var c = t.Construction;
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
                bool perJob = logging.ValueKind == JsonValueKind.Object && logging.TryGetProperty("per_job_stats", out var pjs) ? pjs.GetBoolean() : t.PerJobStatsLogging;
                string level = logging.ValueKind == JsonValueKind.Object && logging.TryGetProperty("level", out var lvl) ? (lvl.GetString() ?? t.LogLevel) : t.LogLevel;

                int carry = t.BackpressureMaxCarryoverTicks;
                if (root.TryGetProperty("backpressure", out var bpEl) && bpEl.ValueKind == JsonValueKind.Object)
                {
                    if (bpEl.TryGetProperty("max_carryover_ticks", out var mc))
                        carry = Math.Max(1, mc.GetInt32());
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
                    BackpressureMaxCarryoverTicks = carry
                };
            }
            catch (Exception ex)
            {
                Logger.Log($"[SCHED.TUNINGS] Failed to load tunings: {ex.Message}");
                return new SchedulerTunings();
            }
        }
    }
}
