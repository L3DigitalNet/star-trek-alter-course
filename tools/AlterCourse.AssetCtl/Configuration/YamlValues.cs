using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace AlterCourse.AssetCtl.Configuration;

/// <summary>Loads the deliberately small AssetCtl YAML dialect without resolving executable or recursive YAML features.</summary>
internal static class YamlValues
{
    private const int MaximumCharacters = 1_048_576;
    private const int MaximumDepth = 32;
    private const int MaximumNodes = 20_000;

    public static YamlMappingNode LoadMapping(string path)
    {
        string text;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (stream.Length > MaximumCharacters)
            {
                throw new AssetCtlException($"{path}: YAML exceeds the {MaximumCharacters}-byte limit.", 2);
            }

            using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
            text = reader.ReadToEnd();
        }

        RejectProhibitedSyntax(path, text);
        var yaml = new YamlStream();
        try
        {
            yaml.Load(new StringReader(text));
        }
        catch (YamlException exception)
        {
            throw new AssetCtlException($"{path}: invalid or duplicate-key YAML: {exception.Message}", 2);
        }

        if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new AssetCtlException($"{path}: expected exactly one YAML mapping document.", 2);
        }

        int count = 0;
        ValidateTree(root, 0, ref count, path);
        return root;
    }

    private static void RejectProhibitedSyntax(string path, string text)
    {
        int lineNumber = 0;
        foreach (string line in text.Split('\n'))
        {
            lineNumber++;
            string content = StripQuotedAndComment(line);
            if (
                content.Contains('&', StringComparison.Ordinal)
                || content.Contains('*', StringComparison.Ordinal)
                || content.Contains('!', StringComparison.Ordinal)
            )
            {
                throw new AssetCtlException($"{path}:{lineNumber}: YAML tags, anchors, and aliases are prohibited.", 2);
            }
        }
    }

    private static string StripQuotedAndComment(string value)
    {
        char[] result = new char[value.Length];
        int output = 0;
        bool single = false;
        bool doubleQuoted = false;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (character == '\'' && !doubleQuoted)
            {
                single = !single;
                continue;
            }

            if (character == '"' && !single && (index == 0 || value[index - 1] != '\\'))
            {
                doubleQuoted = !doubleQuoted;
                continue;
            }

            if (character == '#' && !single && !doubleQuoted)
            {
                break;
            }

            if (!single && !doubleQuoted)
            {
                result[output++] = character;
            }
        }

        return new string(result, 0, output);
    }

    private static void ValidateTree(YamlNode node, int depth, ref int count, string path)
    {
        count++;
        if (depth > MaximumDepth || count > MaximumNodes)
        {
            throw new AssetCtlException($"{path}: YAML structure exceeds safety bounds.", 2);
        }

        switch (node)
        {
            case YamlMappingNode mapping:
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (
                    global::System.Collections.Generic.KeyValuePair<
                        global::YamlDotNet.RepresentationModel.YamlNode,
                        global::YamlDotNet.RepresentationModel.YamlNode
                    > pair in mapping.Children
                )
                {
                    if (pair.Key is not YamlScalarNode { Value: not null } key)
                    {
                        throw new AssetCtlException($"{path}: mapping keys must be strings.", 2);
                    }

                    if (!keys.Add(key.Value))
                    {
                        throw new AssetCtlException($"{path}: duplicate key '{key.Value}'.", 2);
                    }

                    ValidateTree(pair.Value, depth + 1, ref count, path);
                }

                break;
            case YamlSequenceNode sequence:
                foreach (global::YamlDotNet.RepresentationModel.YamlNode child in sequence.Children)
                {
                    ValidateTree(child, depth + 1, ref count, path);
                }

                break;
            case YamlScalarNode:
                break;
            default:
                throw new AssetCtlException($"{path}: aliases and custom YAML nodes are prohibited.", 2);
        }
    }

    public static void RequireOnly(this YamlMappingNode mapping, string path, params string[] keys)
    {
        var allowed = new HashSet<string>(keys, StringComparer.Ordinal);
        foreach (
            global::System.Collections.Generic.KeyValuePair<
                global::YamlDotNet.RepresentationModel.YamlNode,
                global::YamlDotNet.RepresentationModel.YamlNode
            > pair in mapping.Children
        )
        {
            string key = ((YamlScalarNode)pair.Key).Value!;
            if (!allowed.Contains(key))
            {
                throw new AssetCtlException($"{path}.{key}: unknown key.", 2);
            }
        }
    }

    public static string Scalar(this YamlMappingNode mapping, string key, string path)
    {
        if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? node))
        {
            throw new AssetCtlException($"{path}.{key}: required value is missing.", 2);
        }

        return node is YamlScalarNode { Value: not null } scalar
            ? scalar.Value
            : throw new AssetCtlException($"{path}.{key}: expected scalar.", 2);
    }

    public static string? OptionalScalar(this YamlMappingNode mapping, string key, string path)
    {
        if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? node))
        {
            return null;
        }

        if (node is YamlScalarNode { Value: null or "null" })
        {
            return null;
        }

        if (node is not YamlScalarNode scalar)
        {
            throw new AssetCtlException($"{path}.{key}: expected scalar.", 2);
        }

        return scalar.Value is null || string.Equals(scalar.Value, "null", StringComparison.Ordinal)
            ? null
            : scalar.Value;
    }

    public static YamlMappingNode Mapping(this YamlMappingNode mapping, string key, string path)
    {
        if (
            !mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? node)
            || node is not YamlMappingNode result
        )
        {
            throw new AssetCtlException($"{path}.{key}: expected mapping.", 2);
        }

        return result;
    }

    public static YamlMappingNode? OptionalMapping(this YamlMappingNode mapping, string key, string path)
    {
        if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? node))
        {
            return null;
        }

        if (node is YamlScalarNode { Value: null or "null" })
        {
            return null;
        }

        return node as YamlMappingNode ?? throw new AssetCtlException($"{path}.{key}: expected mapping.", 2);
    }

    public static YamlSequenceNode Sequence(this YamlMappingNode mapping, string key, string path)
    {
        if (
            !mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? node)
            || node is not YamlSequenceNode result
        )
        {
            throw new AssetCtlException($"{path}.{key}: expected sequence.", 2);
        }

        return result;
    }

    public static YamlSequenceNode? OptionalSequence(this YamlMappingNode mapping, string key, string path)
    {
        if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? node))
        {
            return null;
        }

        return node as YamlSequenceNode ?? throw new AssetCtlException($"{path}.{key}: expected sequence.", 2);
    }

    public static bool Boolean(this YamlMappingNode mapping, string key, string path) =>
        bool.TryParse(mapping.Scalar(key, path), out bool result)
            ? result
            : throw new AssetCtlException($"{path}.{key}: expected true or false.", 2);

    public static int Integer(this YamlMappingNode mapping, string key, string path) =>
        int.TryParse(mapping.Scalar(key, path), CultureInfo.InvariantCulture, out int result)
            ? result
            : throw new AssetCtlException($"{path}.{key}: expected integer.", 2);

    public static long Long(this YamlMappingNode mapping, string key, string path) =>
        long.TryParse(mapping.Scalar(key, path), CultureInfo.InvariantCulture, out long result)
            ? result
            : throw new AssetCtlException($"{path}.{key}: expected integer.", 2);

    public static decimal Decimal(this YamlMappingNode mapping, string key, string path) =>
        decimal.TryParse(
            mapping.Scalar(key, path),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out decimal result
        )
            ? result
            : throw new AssetCtlException($"{path}.{key}: expected decimal.", 2);

    public static IReadOnlyList<string> Strings(YamlSequenceNode? sequence, string path)
    {
        if (sequence is null)
        {
            return [];
        }

        return sequence
            .Children.Select(
                (node, index) =>
                    node is YamlScalarNode { Value: not null } scalar
                        ? scalar.Value
                        : throw new AssetCtlException($"{path}[{index}]: expected scalar.", 2)
            )
            .ToArray();
    }
}
