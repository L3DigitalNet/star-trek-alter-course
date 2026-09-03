using System.Collections;
using System.Collections.Immutable;

namespace AlterCourse.Core.AI;

/// <summary>Provides immutable sequence-value semantics for decision inputs and explanations.</summary>
internal sealed class ReadOnlyDecisionList<T> : IReadOnlyList<T>, IEquatable<ReadOnlyDecisionList<T>>
{
    private readonly ImmutableArray<T> _items;

    internal ReadOnlyDecisionList(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = [.. items];
    }

    public int Count => _items.Length;

    public T this[int index] => _items[index];

    public bool Equals(ReadOnlyDecisionList<T>? other) => other is not null && _items.SequenceEqual(other._items);

    public override bool Equals(object? obj) => Equals(obj as ReadOnlyDecisionList<T>);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (T item in _items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
