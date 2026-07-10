namespace HumanFortress.Jobs.Transport;

internal sealed partial class TransportJobExecutor
{
    internal void ReadTick(ulong tick)
    {
        _paths.BeginTick();
        int intakeBudget = GetEffectiveIntakeBudget();
        _inboxBuffer.Clear();

        _backlog.DrainInto(intakeBudget, _inboxBuffer);

        if (_inboxBuffer.Count < intakeBudget)
        {
            _requests.Drain(intakeBudget - _inboxBuffer.Count, _inboxBuffer);
        }

        LastIntakeCount = _inboxBuffer.Count;

        if (_inboxBuffer.Count == 0)
        {
            if ((tick % 60UL) == 0UL)
            {
                _logger.Log($"[TRANS-JOBS][{tick}] No requests dequeued.");
            }

            _statsTracker.RecordRead(0, _active.Count, _backlog.Count, 0);
            return;
        }

        var reqs = _intakeFilter.FilterReadyRequests(_inboxBuffer, tick);

        var creatures = _world.Creatures.GetAllInstances().OrderBy(c => c.Guid).ToList();
        int allowedActive = GetAllowedActiveCount(creatures.Count);
        _logger.Log($"[TRANS-JOBS][{tick}] Intake={reqs.Count} Active={_active.Count} Backlog={_backlog.Count} Workers={creatures.Count} MaxActive={(allowedActive == int.MaxValue ? -1 : allowedActive)}");

        var busy = new HashSet<Guid>(_active.Select(a => a.CreatureId));
        bool throttleLogged = false;
        for (int i = 0; i < reqs.Count; i++)
        {
            var rq = reqs[i];
            if (allowedActive != int.MaxValue && _active.Count >= allowedActive)
            {
                if (!throttleLogged && HasActiveThrottle())
                {
                    int cappedReserve = Math.Min(creatures.Count, Math.Max(0, _hintReserveSlots));
                    _logger.Log($"[TRANS-JOBS][{tick}] Throttled assignments: active={_active.Count} limit={allowedActive} reserve={cappedReserve}");
                    throttleLogged = true;
                }

                _statsTracker.RecordRequeued(_backlog.EnqueueRange(reqs, i, tick));
                break;
            }

            var assignedJob = _assignmentHandler.TryAssign(rq, creatures, busy, tick);
            if (assignedJob != null)
            {
                _active.Add(assignedJob);
                busy.Add(assignedJob.CreatureId);
                continue;
            }

            if (_backlog.TryEnqueue(rq, tick))
            {
                _statsTracker.RecordRequeued();
            }
        }

        int carryoverOld = _backlog.CountOlderThan(tick, _carryoverMaxTicks * 2);
        _statsTracker.RecordRead(LastIntakeCount, _active.Count, _backlog.Count, carryoverOld);
    }
}
