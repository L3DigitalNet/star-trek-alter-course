using System.Collections;
using System.Collections.Immutable;

namespace AlterCourse.Core.Player;

/// <summary>Provides immutable sequence-value semantics for freshly built projections.</summary>
internal sealed class ReadOnlyValueList<T> : IReadOnlyList<T>, IEquatable<ReadOnlyValueList<T>>
{
    private readonly ImmutableArray<T> items;

    internal ReadOnlyValueList(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        this.items = [.. items];
    }

    public int Count => items.Length;

    public T this[int index] => items[index];

    public bool Equals(ReadOnlyValueList<T>? other) =>
        other is not null && items.SequenceEqual(other.items);

    public override bool Equals(object? obj) => Equals(obj as ReadOnlyValueList<T>);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (T item in items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
