namespace AlterCourse.AssetCtl.Validation;

internal static class OutputContractPolicy
{
    private const int MaximumPreviewCount = 32;
    private const int MaximumPreviewDimension = 8_192;
    private const int MaximumDimension = 16_384;
    private const long HardMaximumPixels = 16_777_216;

    public static void Validate(OutputContract output, long maximumPixels)
    {
        long effectiveMaximum = Math.Min(maximumPixels, HardMaximumPixels);
        if (
            output.Width <= 0
            || output.Height <= 0
            || output.Width > MaximumDimension
            || output.Height > MaximumDimension
            || !AllowsDimensions(output.Width, output.Height, effectiveMaximum)
            || output.TargetDisplaySizes.Count is < 1 or > MaximumPreviewCount
            || output.TargetDisplaySizes.Any(size =>
                size <= 0 || size > MaximumPreviewDimension || (long)size * size > effectiveMaximum
            )
        )
        {
            throw new AssetCtlException("output dimensions or target previews are outside safety bounds.", 2);
        }
    }

    public static bool AllowsDimensions(int width, int height, long maximumPixels) =>
        width > 0
        && height > 0
        && width <= MaximumDimension
        && height <= MaximumDimension
        && (long)width * height <= Math.Min(maximumPixels, HardMaximumPixels);
}
