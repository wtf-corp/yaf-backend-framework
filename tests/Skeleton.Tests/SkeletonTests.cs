namespace Skeleton.Tests;

public class SkeletonTests
{
    [Fact]
    public void Runtime_Version_IsNet11()
    {
        var major = Environment.Version.Major;

        Assert.Equal(11, major);
    }
}
