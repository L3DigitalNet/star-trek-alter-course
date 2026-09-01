using System.Security.Cryptography;
using AlterCourse.AssetCtl.Review;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Verifies CLI parsing, lifecycle review gates, and diagnostic redaction.</summary>
public sealed class CliAndLifecycleTests
{
    /// <summary>Rejects ambiguous duplicate options and options missing required values.</summary>
    [Fact]
    public void CliRejectsDuplicateAndMissingOptionValues()
    {
        Assert.Throws<AssetCtlException>(() => CliOptions.Parse(["--output", "json", "--output", "human"]));
        var options = CliOptions.Parse(["--output"]);
        Assert.Throws<AssetCtlException>(() => options.Value("output"));
    }

    /// <summary>Preserves semantic hard failures regardless of aggregate score.</summary>
    [Fact]
    public void SemanticHardFailuresCannotBeHiddenByHighScore()
    {
        var result = new SemanticReviewResult(false, true, true, true, 1, 1, [], false, false, 1, "pass", "different-provider-family");
        Assert.True(result.HasHardFailure);
    }

    /// <summary>Rejects reviewer output that is incomplete or outside schema bounds.</summary>
    [Fact]
    public void ReviewerRejectsOutOfRangeMetricsAndMissingFields()
    {
        ProviderException exception = Assert.Throws<ProviderException>(() => OpenAiVisionReviewer.Parse("{\"overall_score\":2}"));
        Assert.Equal(ProviderErrorCategory.MalformedResponse, exception.Category);
    }

    /// <summary>Routes provider diagnostics through the shared credential redactor.</summary>
    [Theory]
    [InlineData("authorization: Bearer ")]
    [InlineData("api_key=")]
    [InlineData("token:")]
    public void EveryProviderDiagnosticUsesSharedRedaction(string prefix)
    {
        string credential = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        Assert.DoesNotContain(credential, Redactor.Sanitize(prefix + credential), StringComparison.OrdinalIgnoreCase);
    }
}
