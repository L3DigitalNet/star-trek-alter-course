using AlterCourse.Core.Identity;

namespace AlterCourse.Core.Orders;

/// <summary>Defines one persistable order attached to a ship by aggregate containment.</summary>
public abstract record ShipOrder
{
    private protected ShipOrder(ShipOrderId id, ShipOrderKind kind)
    {
        if (id.Value <= 0)
        {
            throw new ArgumentException("A ship order requires an initialized identity.", nameof(id));
        }

        Id = id;
        Kind = kind;
    }

    /// <summary>Gets the stable order identity.</summary>
    public ShipOrderId Id { get; }

    /// <summary>Gets the supported order behavior.</summary>
    public ShipOrderKind Kind { get; }
}
