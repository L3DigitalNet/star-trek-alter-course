using System.Reflection;
using System.Reflection.Emit;

namespace AlterCourse.Core.Tests.Support;

/// <summary>
/// Collects every type a method body references, following calls into the methods a caller chooses to descend into.
/// </summary>
/// <remarks>
/// <para>
/// Architecture tests use this to prove what a code path actually touches (for example, that V10 capture never
/// constructs or reads a historical ship DTO) instead of trusting method names. It decodes raw IL with the runtime's
/// own <see cref="OpCodes"/> table, resolves every member/type/token operand, and records the declaring type, return
/// and parameter types, field types, local variable types, and all generic arguments — so a DTO reached through
/// <c>Enumerable.Select&lt;ShipState, ShipSnapshotV9&gt;</c> or a lambda closure is still reported.
/// </para>
/// <para>
/// Limits: an interface or virtual call records the declared target only (the walker cannot know the runtime
/// implementation), and reflection-based or dynamic access is invisible. Production persistence uses neither for DTO
/// construction, which is what makes the static walk sufficient for the contracts that use it.
/// </para>
/// </remarks>
internal static class IlTypeReferenceWalker
{
    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(code => code.Value);

    /// <summary>
    /// Returns each referenced type with the first call path that reached it, walking from <paramref name="roots"/>
    /// and descending into callees accepted by <paramref name="descend"/>.
    /// </summary>
    internal static IReadOnlyDictionary<Type, string> ReferencedTypes(
        IEnumerable<MethodBase> roots,
        Func<MethodBase, bool> descend
    )
    {
        var referenced = new Dictionary<Type, string>();
        var visited = new HashSet<MethodBase>();
        var pending = new Queue<(MethodBase Method, string Path)>();
        foreach (MethodBase root in roots)
        {
            pending.Enqueue((root, Describe(root)));
        }

        while (pending.Count > 0)
        {
            (MethodBase method, string path) = pending.Dequeue();
            if (!visited.Add(method))
            {
                continue;
            }

            foreach (MemberInfo member in Operands(method))
            {
                foreach (Type type in TypesOf(member))
                {
                    Record(referenced, type, path);
                }

                if (member is MethodBase callee && !visited.Contains(callee) && descend(callee))
                {
                    pending.Enqueue((callee, path + " -> " + Describe(callee)));
                }
            }

            foreach (LocalVariableInfo local in method.GetMethodBody()?.LocalVariables ?? [])
            {
                Record(referenced, local.LocalType, path);
            }
        }

        return referenced;
    }

    private static IEnumerable<MemberInfo> Operands(MethodBase method)
    {
        byte[]? il = method.GetMethodBody()?.GetILAsByteArray();
        if (il is null)
        {
            yield break;
        }

        Type[]? typeArguments = method.DeclaringType is { IsGenericType: true } declaring
            ? declaring.GetGenericArguments()
            : null;
        Type[]? methodArguments = method is MethodInfo { IsGenericMethod: true } generic
            ? generic.GetGenericArguments()
            : null;
        int offset = 0;
        while (offset < il.Length)
        {
            short value = il[offset] == 0xFE ? unchecked((short)(0xFE00 | il[offset + 1])) : il[offset];
            offset += il[offset] == 0xFE ? 2 : 1;
            OpCode code = OpCodesByValue[value];
            switch (code.OperandType)
            {
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                    // A resolution failure is allowed to throw: skipping an unresolvable operand would silently hide
                    // exactly the reference an architecture test exists to find.
                    yield return method.Module.ResolveMember(
                        BitConverter.ToInt32(il, offset),
                        typeArguments,
                        methodArguments
                    )!;
                    offset += 4;
                    break;
                case OperandType.InlineSwitch:
                    offset += 4 + (4 * BitConverter.ToInt32(il, offset));
                    break;
                default:
                    offset += OperandSize(code.OperandType);
                    break;
            }
        }
    }

    private static int OperandSize(OperandType operandType) =>
        operandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            _ => 4,
        };

    private static IEnumerable<Type> TypesOf(MemberInfo member)
    {
        switch (member)
        {
            case Type type:
                yield return type;
                break;
            case FieldInfo field:
                yield return field.DeclaringType!;
                yield return field.FieldType;
                break;
            case MethodBase method:
                if (method.DeclaringType is not null)
                {
                    yield return method.DeclaringType;
                }

                if (method is MethodInfo info)
                {
                    yield return info.ReturnType;
                    if (info.IsGenericMethod)
                    {
                        foreach (Type argument in info.GetGenericArguments())
                        {
                            yield return argument;
                        }
                    }
                }

                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    yield return parameter.ParameterType;
                }

                break;
        }
    }

    private static void Record(Dictionary<Type, string> referenced, Type type, string path)
    {
        if (type.IsByRef || type.IsPointer || type.IsArray)
        {
            Record(referenced, type.GetElementType()!, path);
            return;
        }

        if (!referenced.TryAdd(type, path))
        {
            return;
        }

        if (type.IsGenericType)
        {
            foreach (Type argument in type.GetGenericArguments())
            {
                Record(referenced, argument, path);
            }
        }
    }

    private static string Describe(MethodBase method) => $"{method.DeclaringType?.Name}.{method.Name}";
}
