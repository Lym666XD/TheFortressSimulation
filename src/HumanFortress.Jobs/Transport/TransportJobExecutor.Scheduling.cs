namespace HumanFortress.Jobs.Transport;

internal sealed partial class TransportJobExecutor
{
    internal void ApplySchedulingHints(int? intakeCap, int? maxActiveCap, int reserveSlots)
    {
        _hintIntakeCap = intakeCap;
        _hintMaxActive = maxActiveCap;
        _hintReserveSlots = Math.Max(0, reserveSlots);
    }

    private int GetEffectiveIntakeBudget()
    {
        int budget = _configuredIntakePerTick;
        if (_hintIntakeCap.HasValue)
        {
            int cap = Math.Max(1, _hintIntakeCap.Value);
            budget = Math.Min(budget, cap);
        }

        return budget;
    }

    private int GetAllowedActiveCount(int totalWorkers)
    {
        int allowed = _configuredMaxActive > 0 ? _configuredMaxActive : int.MaxValue;
        if (_hintMaxActive.HasValue)
        {
            int cap = Math.Max(0, _hintMaxActive.Value);
            allowed = allowed == int.MaxValue ? cap : Math.Min(allowed, cap);
        }

        int reserve = Math.Min(totalWorkers, Math.Max(0, _hintReserveSlots));
        if (allowed == int.MaxValue)
        {
            if (reserve == 0)
            {
                return int.MaxValue;
            }

            int res = totalWorkers - reserve;
            return res < 0 ? 0 : res;
        }

        allowed = Math.Min(allowed, totalWorkers);
        allowed -= reserve;
        if (allowed < 0)
        {
            allowed = 0;
        }

        return allowed;
    }

    private bool HasActiveThrottle()
    {
        return _configuredMaxActive > 0
            || _hintReserveSlots > 0
            || (_hintMaxActive.HasValue && _hintMaxActive.Value > 0);
    }
}
