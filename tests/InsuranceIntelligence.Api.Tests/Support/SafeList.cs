using System.Collections;

namespace InsuranceIntelligence.Api.Tests.Support;

/// <summary>
/// A list several tasks can add to and read at once, for test doubles that stand in for a shared store. Reads see a snapshot, so a
/// test that serves many customers at the same moment is not undone by the double's own bookkeeping.
/// </summary>
public sealed class SafeList<T> : IList<T>, IReadOnlyList<T>
{
    private readonly List<T> items = [];
    private readonly object gate = new();

    private T[] Snapshot()
    {
        lock (gate)
        {
            return items.ToArray();
        }
    }

    public T this[int index]
    {
        get { lock (gate) { return items[index]; } }
        set { lock (gate) { items[index] = value; } }
    }

    public int Count { get { lock (gate) { return items.Count; } } }

    public bool IsReadOnly => false;

    public void Add(T item) { lock (gate) { items.Add(item); } }

    public void Clear() { lock (gate) { items.Clear(); } }

    public bool Contains(T item) { lock (gate) { return items.Contains(item); } }

    public void CopyTo(T[] array, int arrayIndex) { lock (gate) { items.CopyTo(array, arrayIndex); } }

    public int IndexOf(T item) { lock (gate) { return items.IndexOf(item); } }

    public void Insert(int index, T item) { lock (gate) { items.Insert(index, item); } }

    public bool Remove(T item) { lock (gate) { return items.Remove(item); } }

    public void RemoveAt(int index) { lock (gate) { items.RemoveAt(index); } }

    public int FindIndex(Predicate<T> match) { lock (gate) { return items.FindIndex(match); } }

    public int RemoveAll(Predicate<T> match) { lock (gate) { return items.RemoveAll(match); } }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Snapshot()).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
