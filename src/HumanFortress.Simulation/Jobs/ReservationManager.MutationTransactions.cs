namespace HumanFortress.Simulation.Jobs;

internal sealed partial class ReservationManager
{
    internal MutationScope BeginMutationScope()
    {
        lock (_sync)
        {
            return new MutationScope(
                this,
                _itemReservations.ToDictionary(
                    static entry => entry.Key,
                    static entry => Clone(entry.Value)),
                _creatureReservations.ToDictionary(
                    static entry => entry.Key,
                    static entry => Clone(entry.Value)),
                _nextGeneration);
        }
    }

    private static ItemReservation Clone(ItemReservation source)
    {
        return new ItemReservation
        {
            Token = source.Token,
            ExpireTick = source.ExpireTick,
            IsStagedTransfer = source.IsStagedTransfer,
            TransferSourceId = source.TransferSourceId,
            TransferSourceGeneration = source.TransferSourceGeneration
        };
    }

    private static CreatureReservation Clone(CreatureReservation source)
    {
        return new CreatureReservation
        {
            Token = source.Token,
            ExpireTick = source.ExpireTick
        };
    }

    internal sealed class MutationScope : IDisposable
    {
        private readonly ReservationManager _owner;
        private Dictionary<Guid, ItemReservation>? _items;
        private Dictionary<Guid, CreatureReservation>? _creatures;
        private readonly ulong _nextGeneration;
        private bool _committed;

        internal MutationScope(
            ReservationManager owner,
            Dictionary<Guid, ItemReservation> items,
            Dictionary<Guid, CreatureReservation> creatures,
            ulong nextGeneration)
        {
            _owner = owner;
            _items = items;
            _creatures = creatures;
            _nextGeneration = nextGeneration;
        }

        internal void Commit()
        {
            ObjectDisposedException.ThrowIf(_items == null, this);
            _committed = true;
        }

        void IDisposable.Dispose()
        {
            var items = Interlocked.Exchange(ref _items, null);
            var creatures = Interlocked.Exchange(ref _creatures, null);
            if (items == null || creatures == null || _committed)
                return;

            lock (_owner._sync)
            {
                _owner._itemReservations.Clear();
                foreach (var entry in items)
                    _owner._itemReservations.Add(entry.Key, Clone(entry.Value));

                _owner._creatureReservations.Clear();
                foreach (var entry in creatures)
                    _owner._creatureReservations.Add(entry.Key, Clone(entry.Value));

                _owner._nextGeneration = _nextGeneration;
            }
        }
    }
}
