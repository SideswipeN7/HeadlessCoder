using HeadlessCoder.Auth;

namespace HeadlessCoder.Tests;

public class SecurityUtilTests
{
    [Fact]
    public void FixedEquals_ReturnsTrue_ForIdenticalStrings()
    {
        Assert.True(SecurityUtil.FixedEquals("Bumblebee742", "Bumblebee742"));
    }

    [Fact]
    public void FixedEquals_ReturnsTrue_ForTwoEmptyStrings()
    {
        Assert.True(SecurityUtil.FixedEquals("", ""));
    }

    [Fact]
    public void FixedEquals_ReturnsFalse_ForDifferentSameLengthStrings()
    {
        Assert.False(SecurityUtil.FixedEquals("Bumblebee742", "Bumblebee743"));
    }

    [Fact]
    public void FixedEquals_ReturnsFalse_ForDifferentLengthStrings()
    {
        Assert.False(SecurityUtil.FixedEquals("secret", "secretx"));
    }

    [Fact]
    public void FixedEquals_IsCaseSensitive()
    {
        Assert.False(SecurityUtil.FixedEquals("Optimus", "optimus"));
    }

    [Fact]
    public void FixedEquals_HandlesMultiByteUtf8()
    {
        Assert.True(SecurityUtil.FixedEquals("później-ä-🚗", "później-ä-🚗"));
        Assert.False(SecurityUtil.FixedEquals("później-ä-🚗", "później-a-🚗"));
    }
}
