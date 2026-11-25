using YAF.Testing;

namespace Testing.Tests.Approvals;

// The namespace of this folder hides the class name, so the helper is called by its full name.
public sealed class ApprovalsTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("yaf-approvals-");

    private string CallerFilePath => Path.Combine(_directory.FullName, "CatalogTests.cs");

    private string ApprovedPath => Path.Combine(_directory.FullName, "errors.approved.txt");

    private string ReceivedPath => Path.Combine(_directory.FullName, "errors.received.txt");

    public void Dispose() => _directory.Delete(recursive: true);

    #region approvals
    [Fact]
    public void Verify_CommittedApprovedFile_Passes()
    {
        var actual = "Approval files hold codes and names.\nThis one proves that the calling file is found in CI.\n";

        var act = () => YAF.Testing.Approvals.Verify(actual, "sample");

        act.Should().NotThrow();
    }
    #endregion

    [Fact]
    public void Verify_EqualText_Passes()
    {
        File.WriteAllText(ApprovedPath, "catalog.session.full\ncatalog.session.title_required\n");

        var act = () => YAF.Testing.Approvals.Verify("catalog.session.full\ncatalog.session.title_required\n", "errors", CallerFilePath);

        act.Should().NotThrow();
        File.Exists(ReceivedPath).Should().BeFalse();
    }

    [Fact]
    public void Verify_DifferentText_WritesReceivedAndListsAddedAndRemovedLines()
    {
        File.WriteAllText(ApprovedPath, "catalog.session.full\ncatalog.session.title_required\n");
        var actual = "catalog.session.full\ncatalog.session.capacity_exceeded\n";

        var act = () => YAF.Testing.Approvals.Verify(actual, "errors", CallerFilePath);

        act.Should().Throw<YafAssertionException>()
            .Which.Message.Should().Contain("+ catalog.session.capacity_exceeded")
            .And.Contain("- catalog.session.title_required")
            .And.NotContain("catalog.session.full");
        File.ReadAllText(ReceivedPath).Should().Be(actual);
    }

    [Fact]
    public void Verify_NoApprovedFile_WritesReceivedAndFails()
    {
        var actual = "catalog.session.full\n";

        var act = () => YAF.Testing.Approvals.Verify(actual, "errors", CallerFilePath);

        act.Should().Throw<YafAssertionException>()
            .Which.Message.Should().Contain("errors.approved.txt").And.Contain("+ catalog.session.full");
        File.ReadAllText(ReceivedPath).Should().Be(actual);
    }

    [Fact]
    public void Verify_EqualText_DeletesAStaleReceivedFile()
    {
        File.WriteAllText(ApprovedPath, "catalog.session.full\n");
        File.WriteAllText(ReceivedPath, "catalog.session.title_required\n");

        YAF.Testing.Approvals.Verify("catalog.session.full\n", "errors", CallerFilePath);

        File.Exists(ReceivedPath).Should().BeFalse();
    }

    [Fact]
    public void Verify_OtherLineEnds_Passes()
    {
        File.WriteAllText(ApprovedPath, "catalog.session.full\r\ncatalog.session.title_required\r\n");

        var act = () => YAF.Testing.Approvals.Verify("catalog.session.full\ncatalog.session.title_required\n", "errors", CallerFilePath);

        act.Should().NotThrow();
    }

    [Fact]
    public void Verify_MissingCallerDirectory_NamesDeterministicSourcePaths()
    {
        var callerFilePath = Path.Combine(_directory.FullName, "missing", "CatalogTests.cs");

        var act = () => YAF.Testing.Approvals.Verify("catalog.session.full\n", "errors", callerFilePath);

        act.Should().Throw<YafAssertionException>().Which.Message.Should().Contain("DeterministicSourcePaths");
    }
}
