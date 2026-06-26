namespace HumanFortress.Navigation;

/// <summary>
/// Binary min-heap for A* open set.
/// Deterministic ordering per NAVIGATION_SPEC.md section 5.1.
/// </summary>
internal sealed class BinaryHeap
{
    private HeapEntry[] _heap;
    private int _count;

    internal BinaryHeap(int capacity)
    {
        _heap = new HeapEntry[capacity];
        _count = 0;
    }

    internal int Count => _count;

    internal void Clear()
    {
        _count = 0;
    }

    internal void Push(HeapEntry entry)
    {
        // Grow if needed
        if (_count >= _heap.Length)
        {
            Array.Resize(ref _heap, _heap.Length * 2);
        }

        // Add to end and bubble up
        _heap[_count] = entry;
        BubbleUp(_count);
        _count++;
    }

    internal HeapEntry Pop()
    {
        if (_count == 0)
            throw new InvalidOperationException("Heap is empty");

        var result = _heap[0];

        // Move last element to root and bubble down
        _count--;
        if (_count > 0)
        {
            _heap[0] = _heap[_count];
            BubbleDown(0);
        }

        return result;
    }

    internal IEnumerable<HeapEntry> GetAll()
    {
        for (int i = 0; i < _count; i++)
        {
            yield return _heap[i];
        }
    }

    private void BubbleUp(int index)
    {
        while (index > 0)
        {
            int parentIdx = (index - 1) / 2;

            if (CompareEntries(_heap[index], _heap[parentIdx]) < 0)
            {
                Swap(index, parentIdx);
                index = parentIdx;
            }
            else
            {
                break;
            }
        }
    }

    private void BubbleDown(int index)
    {
        while (true)
        {
            int leftChild = index * 2 + 1;
            int rightChild = index * 2 + 2;
            int smallest = index;

            if (leftChild < _count && CompareEntries(_heap[leftChild], _heap[smallest]) < 0)
            {
                smallest = leftChild;
            }

            if (rightChild < _count && CompareEntries(_heap[rightChild], _heap[smallest]) < 0)
            {
                smallest = rightChild;
            }

            if (smallest != index)
            {
                Swap(index, smallest);
                index = smallest;
            }
            else
            {
                break;
            }
        }
    }

    private void Swap(int i, int j)
    {
        (_heap[i], _heap[j]) = (_heap[j], _heap[i]);
    }

    /// <summary>
    /// Compare entries using deterministic tie-breakers.
    /// Per NAVIGATION_SPEC.md section 5.1:
    /// 1. Smaller f = g + h
    /// 2. Smaller h
    /// 3. Smaller g
    /// 4. Smaller LocalIndex (row-major; 0..1023)
    /// </summary>
    private static int CompareEntries(HeapEntry a, HeapEntry b)
    {
        // Compare F
        if (a.F != b.F)
            return a.F.CompareTo(b.F);

        // Compare H
        if (a.H != b.H)
            return a.H.CompareTo(b.H);

        // Compare G
        if (a.G != b.G)
            return a.G.CompareTo(b.G);

        // Compare LocalIndex
        return a.LocalIndex.CompareTo(b.LocalIndex);
    }
}

/// <summary>
/// Entry in the A* open set heap.
/// </summary>
internal readonly struct HeapEntry
{
    internal readonly ulong Key;      // Node identifier
    internal readonly uint F;         // f = g + h (scaled fixed-point)
    internal readonly uint H;         // Heuristic (scaled fixed-point)
    internal readonly uint G;         // Cost from start (scaled fixed-point)
    internal readonly int LocalIndex; // Deterministic tie-breaker

    internal HeapEntry(ulong key, uint f, uint h, uint g, int localIndex)
    {
        Key = key;
        F = f;
        H = h;
        G = g;
        LocalIndex = localIndex;
    }
}
