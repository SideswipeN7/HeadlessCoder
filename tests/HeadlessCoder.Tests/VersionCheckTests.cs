namespace HeadlessCoder.Tests;

public class VersionCheckTests
{
    [Theory]
    [InlineData("0.0.5", "v0.0.6")]
    [InlineData("0.0.5", "0.0.6")]      // tag without leading 'v'
    [InlineData("0.0.5", "V1.0.0")]     // uppercase V
    [InlineData("1.0.0", "v2")]         // major only, gets padded
    [InlineData("1.2.3", "v1.3.0")]
    public void IsNewer_ReturnsTrue_WhenTagIsHigher(string current, string tag)
    {
        Assert.True(VersionCheck.IsNewer(current, tag));
    }

    [Theory]
    [InlineData("0.0.5", "v0.0.5")]     // equal
    [InlineData("0.0.5", "v0.0.4")]     // older
    [InlineData("2.0.0", "v1.9.9")]
    [InlineData("1.2.3", "v1.2.3-beta")] // pre-release of the same version is not newer
    public void IsNewer_ReturnsFalse_WhenTagIsSameOrOlder(string current, string tag)
    {
        Assert.False(VersionCheck.IsNewer(current, tag));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsNewer_ReturnsFalse_ForMissingTag(string? tag)
    {
        Assert.False(VersionCheck.IsNewer("1.0.0", tag));
    }

    [Theory]
    [InlineData("not-a-version")]
    [InlineData("vlatest")]
    public void IsNewer_ReturnsFalse_ForUnparseableTag(string tag)
    {
        Assert.False(VersionCheck.IsNewer("1.0.0", tag));
    }

    [Fact]
    public void IsNewer_DropsBuildMetadataFromTag()
    {
        Assert.True(VersionCheck.IsNewer("1.0.0", "v1.1.0+build.42"));
        Assert.False(VersionCheck.IsNewer("1.1.0", "v1.1.0+build.42"));
    }
}
