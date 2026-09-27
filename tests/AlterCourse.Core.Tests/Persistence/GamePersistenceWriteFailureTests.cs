using AlterCourse.Core.Persistence;
using AlterCourse.Core.Tests.Gameplay;

namespace AlterCourse.Core.Tests.Persistence;

/// <summary>Characterizes the save transaction under deterministic file-operation failures.</summary>
public sealed class GamePersistenceWriteFailureTests
{
    /// <summary>Preserves the previous slot and the primary exception at every failing transaction stage.</summary>
    [Theory]
    [InlineData("create", false)]
    [InlineData("write", false)]
    [InlineData("flush", false)]
    [InlineData("replace", false)]
    [InlineData("write", true)]
    [InlineData("flush", true)]
    [InlineData("replace", true)]
    public void FailedSavePreservesPreviousFileAndPrimaryFailure(string stage, bool cleanupFails)
    {
        string directory = Path.Combine(Path.GetTempPath(), "save-failure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "slot.json");
            var fixture = new Milestone3ProofFixture();
            GamePersistence.Save(path, fixture.CreateDefault(), Milestone3ProofFixture.Metadata);
            byte[] previous = File.ReadAllBytes(path);
            var operations = new FailingOperations(stage, cleanupFails);

            GamePersistenceException failure = Assert.Throws<GamePersistenceException>(() =>
                GamePersistence.Save(path, fixture.CreateDefault(), Milestone3ProofFixture.Metadata, operations)
            );

            Assert.Equal(GamePersistenceFailure.InputOutput, failure.Failure);
            Assert.Same(operations.PrimaryFailure, failure.InnerException);
            Assert.Equal(previous, File.ReadAllBytes(path));
            _ = GamePersistence.Load(path, fixture.Catalog);
            Assert.Equal(cleanupFails ? 1 : 0, Directory.GetFiles(directory, "*.tmp").Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class FailingOperations(string stage, bool cleanupFails) : SaveFileOperations
    {
        internal IOException PrimaryFailure { get; } = new("injected " + stage);

        internal override Stream CreateCandidate(string path)
        {
            Fail("create");
            return base.CreateCandidate(path);
        }

        internal override void Write(Stream stream, ReadOnlySpan<byte> json)
        {
            if (string.Equals(stage, "write", StringComparison.Ordinal))
            {
                stream.Write(json[..(json.Length / 2)]);
                throw PrimaryFailure;
            }
            base.Write(stream, json);
        }

        internal override void Flush(Stream stream)
        {
            Fail("flush");
            base.Flush(stream);
        }

        internal override void Replace(string candidate, string target)
        {
            Fail("replace");
            base.Replace(candidate, target);
        }

        internal override void DeleteCandidate(string path)
        {
            if (cleanupFails)
                throw new IOException("injected cleanup");
            base.DeleteCandidate(path);
        }

        private void Fail(string operation)
        {
            if (string.Equals(stage, operation, StringComparison.Ordinal))
                throw PrimaryFailure;
        }
    }
}
