using System;
using System.Text.Json;

namespace HumanFortress.App.Jobs
{
    /// <summary>
    /// Tunings for workshops and crafting (v1 minimal).
    /// Loaded from content/registries/tuning.workshops.json if present.
    /// </summary>
    public sealed class WorkshopTunings
    {
        public int MaxQueuedRecipesDefault { get; init; } = 10;
        public int CraftTicksPerVolume { get; init; } = 1; // ticks per ml (toy model)
        public int WorkersPerWorkshop { get; init; } = 1;
        public int IoScanRadius { get; init; } = 2; // scan around footprint for IO cells
        public bool CheckAttachments { get; init; } = false; // if false, recipes ignore attachment requirements (for debugging)

        public static WorkshopTunings LoadFromJson(string? json, string source)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new WorkshopTunings();
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                return LoadFromJsonElement(doc.RootElement);
            }
            catch (Exception ex)
            {
                Logger.Log($"[WORKSHOP.TUNINGS] Failed to load tunings from {source}: {ex.Message}");
                return new WorkshopTunings();
            }
        }

        private static WorkshopTunings LoadFromJsonElement(JsonElement root)
        {
            var t = new WorkshopTunings();

            int maxQ = root.TryGetProperty("max_queued_recipes_default", out var q) ? q.GetInt32() : t.MaxQueuedRecipesDefault;
            int ctpv = root.TryGetProperty("craft_ticks_per_volume", out var c) ? c.GetInt32() : t.CraftTicksPerVolume;
            int workers = root.TryGetProperty("workers_per_workshop", out var w) ? w.GetInt32() : t.WorkersPerWorkshop;
            int io = root.TryGetProperty("io_scan_radius", out var ioEl) ? ioEl.GetInt32() : t.IoScanRadius;
            bool checkAtt = root.TryGetProperty("check_attachments", out var ca) ? ca.GetBoolean() : t.CheckAttachments;

            return new WorkshopTunings
            {
                MaxQueuedRecipesDefault = Math.Max(0, maxQ),
                CraftTicksPerVolume = Math.Max(1, ctpv),
                WorkersPerWorkshop = Math.Clamp(workers, 1, 8),
                IoScanRadius = Math.Clamp(io, 0, 8),
                CheckAttachments = checkAtt
            };
        }
    }
}
