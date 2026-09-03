using System.Globalization;
using AlterCourse.Godot.Gameplay;
using Godot;

namespace AlterCourse.Godot.Tests;

/// <summary>
/// Captures a deterministic preview from the production game scene and exits with a machine-readable result.
/// </summary>
/// <remarks>
/// Run this development-only scene with Godot user arguments after <c>--</c>. The harness requires a
/// rendering-capable Godot 4.7.2 runtime because it reads the rendered viewport after FramePostDraw.
/// </remarks>
public partial class UiPreviewCapture : Node
{
    private const string MainScenePath = "res://Main.tscn";
    private const string DefaultMode = "travel";
    private const int DefaultWidth = 1920;
    private const int DefaultHeight = 1080;
    private const int MinimumWidth = 320;
    private const int MinimumHeight = 180;
    private const int MaximumWidth = 8192;
    private const int MaximumHeight = 8192;
    private const int SettleFrameCount = 4;

    /// <inheritdoc />
    public override async void _Ready()
    {
        if (!TryParseArguments(OS.GetCmdlineUserArgs(), out CaptureOptions options, out string errorCode))
        {
            Fail(2, errorCode);
            return;
        }

        try
        {
            await CaptureAsync(options);
        }
        catch (Exception exception)
        {
            // Diagnostics expose only the exception type; arbitrary argument values and local paths stay out
            // of failure logs while the fixed code remains suitable for test-runner classification.
            Fail(6, $"unexpected_{exception.GetType().Name.ToLowerInvariant()}");
        }
    }

    private async Task CaptureAsync(CaptureOptions options)
    {
        GetWindow().Size = new Vector2I(options.Width, options.Height);

        PackedScene? mainScene = GD.Load<PackedScene>(MainScenePath);
        if (mainScene is null)
        {
            Fail(3, "main_scene_load_failed");
            return;
        }

        GameScreen gameScreen = mainScene.Instantiate<GameScreen>();
        AddChild(gameScreen);

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!gameScreen.IsNodeReady())
        {
            Fail(3, "main_scene_not_ready");
            return;
        }

        gameScreen.ShowPreview(options.DataMode);

        // Containers, deferred focus, font glyphs, and custom drawing settle on separate engine passes.
        // Capturing earlier makes the fixture depend on scene-entry timing instead of the final UI.
        SignalAwaiter framePostDraw = ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        for (int frame = 0; frame < SettleFrameCount; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        // Subscribe before forcing a draw: headless mode does not schedule another rendered frame on
        // its own, and subscribing after ForceDraw would miss the synchronous FramePostDraw emission.
        RenderingServer.ForceDraw(false);
        await framePostDraw;
        Image? image = GetViewport().GetTexture().GetImage();
        if (image is null || image.IsEmpty())
        {
            Fail(4, "viewport_image_unavailable");
            return;
        }

        if (image.GetWidth() != options.Width || image.GetHeight() != options.Height)
        {
            Fail(4, "viewport_size_mismatch");
            return;
        }

        SaveCapture(image, options);
    }

    private void SaveCapture(Image image, CaptureOptions options)
    {
        string? outputDirectory = Path.GetDirectoryName(options.ResolvedOutputPath);
        if (string.IsNullOrEmpty(outputDirectory))
        {
            Fail(5, "output_directory_invalid");
            return;
        }

        Directory.CreateDirectory(outputDirectory);
        Error saveError = image.SavePng(options.ResolvedOutputPath);
        if (saveError != Error.Ok)
        {
            Fail(5, $"save_failed_{saveError.ToString().ToLowerInvariant()}");
            return;
        }

        GD.Print(
            "ALTER_COURSE_UI_CAPTURE_OK"
                + $" mode={options.ModeName}"
                + $" width={options.Width.ToString(CultureInfo.InvariantCulture)}"
                + $" height={options.Height.ToString(CultureInfo.InvariantCulture)}"
                + $" output={Uri.EscapeDataString(options.RequestedOutputPath)}"
        );
        GetTree().Quit(0);
    }

    private static bool TryParseArguments(string[] arguments, out CaptureOptions options, out string errorCode)
    {
        if (!TryParseArgumentValues(arguments, out Dictionary<string, string> values, out errorCode))
        {
            options = null!;
            return false;
        }

        string modeName = values.GetValueOrDefault("--preview", DefaultMode);
        if (!TryResolveMode(modeName, out CommandInterfaceDataMode dataMode))
        {
            return Invalid(out options, out errorCode, "preview_invalid");
        }

        if (!TryGetDimensions(values, out int width, out int height, out errorCode))
        {
            options = null!;
            return false;
        }

        return TryCreateOptions(values, modeName, dataMode, width, height, out options, out errorCode);
    }

    private static bool TryParseArgumentValues(
        string[] arguments,
        out Dictionary<string, string> values,
        out string errorCode
    )
    {
        values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < arguments.Length; index++)
        {
            string argument = arguments[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                return InvalidValues(out values, out errorCode, "argument_format_invalid");
            }

            int separator = argument.IndexOf('=', StringComparison.Ordinal);
            string key = separator >= 0 ? argument[..separator] : argument;
            string value;
            if (separator >= 0)
            {
                value = argument[(separator + 1)..];
            }
            else if (++index < arguments.Length)
            {
                value = arguments[index];
            }
            else
            {
                return InvalidValues(out values, out errorCode, "argument_value_missing");
            }

            if (key is not ("--preview" or "--width" or "--height" or "--output"))
            {
                return InvalidValues(out values, out errorCode, "argument_unknown");
            }

            if (string.IsNullOrEmpty(value) || !values.TryAdd(key, value))
            {
                return InvalidValues(out values, out errorCode, "argument_value_invalid");
            }
        }

        errorCode = string.Empty;
        return true;
    }

    private static bool TryGetDimensions(
        Dictionary<string, string> values,
        out int width,
        out int height,
        out string errorCode
    )
    {
        string defaultWidth = DefaultWidth.ToString(CultureInfo.InvariantCulture);
        if (
            !TryParseDimension(values.GetValueOrDefault("--width", defaultWidth), MinimumWidth, MaximumWidth, out width)
        )
        {
            height = 0;
            errorCode = "width_invalid";
            return false;
        }

        string defaultHeight = DefaultHeight.ToString(CultureInfo.InvariantCulture);
        if (
            !TryParseDimension(
                values.GetValueOrDefault("--height", defaultHeight),
                MinimumHeight,
                MaximumHeight,
                out height
            )
        )
        {
            errorCode = "height_invalid";
            return false;
        }

        errorCode = string.Empty;
        return true;
    }

    private static bool TryCreateOptions(
        Dictionary<string, string> values,
        string modeName,
        CommandInterfaceDataMode dataMode,
        int width,
        int height,
        out CaptureOptions options,
        out string errorCode
    )
    {
        string requestedOutputPath = values.GetValueOrDefault(
            "--output",
            $"user://ui-preview-{modeName}-{width.ToString(CultureInfo.InvariantCulture)}x{height.ToString(CultureInfo.InvariantCulture)}.png"
        );
        if (!TryResolveOutputPath(requestedOutputPath, out string resolvedOutputPath))
        {
            return Invalid(out options, out errorCode, "output_invalid");
        }

        options = new CaptureOptions(modeName, dataMode, width, height, requestedOutputPath, resolvedOutputPath);
        errorCode = string.Empty;
        return true;
    }

    private static bool TryResolveMode(string modeName, out CommandInterfaceDataMode dataMode)
    {
        dataMode = modeName switch
        {
            "travel" => CommandInterfaceDataMode.TravelPreview,
            "combat" => CommandInterfaceDataMode.CombatPreview,
            "engineering" => CommandInterfaceDataMode.EngineeringPreview,
            _ => CommandInterfaceDataMode.Live,
        };
        return dataMode != CommandInterfaceDataMode.Live;
    }

    private static bool TryParseDimension(string value, int minimum, int maximum, out int dimension) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out dimension)
        && dimension >= minimum
        && dimension <= maximum;

    private static bool TryResolveOutputPath(string requestedPath, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (
            requestedPath.Contains('\r', StringComparison.Ordinal)
            || requestedPath.Contains('\n', StringComparison.Ordinal)
            || !string.Equals(Path.GetExtension(requestedPath), ".png", StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        try
        {
            if (requestedPath.StartsWith("res://", StringComparison.Ordinal))
            {
                return TryResolveContainedPath("res://", requestedPath, out resolvedPath);
            }

            if (requestedPath.StartsWith("user://", StringComparison.Ordinal))
            {
                return TryResolveContainedPath("user://", requestedPath, out resolvedPath);
            }

            if (!Path.IsPathFullyQualified(requestedPath))
            {
                return false;
            }

            resolvedPath = Path.GetFullPath(requestedPath);
            return true;
        }
        catch (Exception exception)
            when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool TryResolveContainedPath(string boundaryPath, string requestedPath, out string resolvedPath)
    {
        string boundary = Path.GetFullPath(ProjectSettings.GlobalizePath(boundaryPath));
        resolvedPath = Path.GetFullPath(ProjectSettings.GlobalizePath(requestedPath));
        string relativePath = Path.GetRelativePath(boundary, resolvedPath);
        return !Path.IsPathRooted(relativePath)
            && !relativePath.Equals("..", StringComparison.Ordinal)
            && !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static bool Invalid(out CaptureOptions options, out string errorCode, string requestedErrorCode)
    {
        options = null!;
        errorCode = requestedErrorCode;
        return false;
    }

    private static bool InvalidValues(
        out Dictionary<string, string> values,
        out string errorCode,
        string requestedErrorCode
    )
    {
        values = null!;
        errorCode = requestedErrorCode;
        return false;
    }

    private void Fail(int exitCode, string errorCode)
    {
        GD.PrintErr($"ALTER_COURSE_UI_CAPTURE_ERROR code={errorCode}");
        GetTree().Quit(exitCode);
    }

    private sealed record CaptureOptions(
        string ModeName,
        CommandInterfaceDataMode DataMode,
        int Width,
        int Height,
        string RequestedOutputPath,
        string ResolvedOutputPath
    );
}
