using System.Text;
using HeadlessCoder.Hosting;

namespace HeadlessCoder.Tests;

public class EmbeddedAssetsTests
{
    [Theory]
    [InlineData("index.html", "text/html; charset=utf-8")]
    [InlineData("login.html", "text/html; charset=utf-8")]
    [InlineData("app.js", "text/javascript; charset=utf-8")]
    [InlineData("styles.css", "text/css; charset=utf-8")]
    [InlineData("themes.css", "text/css; charset=utf-8")]
    [InlineData("logo.svg", "image/svg+xml")]
    public void TryGet_ReturnsEmbeddedWebAsset_WithCorrectContentType(string file, string contentType)
    {
        var asset = EmbeddedAssets.TryGet(file);

        Assert.NotNull(asset);
        Assert.True(asset.Value.Bytes.Length > 0);
        Assert.Equal(contentType, asset.Value.ContentType);
    }

    [Fact]
    public void TryGet_ReturnsNull_ForUnknownAsset()
    {
        Assert.Null(EmbeddedAssets.TryGet("does-not-exist.xyz"));
    }

    [Fact]
    public void TryGet_IsCaseInsensitive()
    {
        Assert.NotNull(EmbeddedAssets.TryGet("INDEX.HTML"));
    }

    [Fact]
    public void TryGet_FindsRootEmbeddedAsciiArt()
    {
        // ascii-art.txt is embedded from the repo root (linked), not the Web folder.
        var asset = EmbeddedAssets.TryGet("ascii-art.txt");
        Assert.NotNull(asset);
        Assert.True(asset.Value.Bytes.Length > 0);
    }

    [Fact]
    public void TryGet_IndexHtml_LooksLikeHtml()
    {
        var asset = EmbeddedAssets.TryGet("index.html");
        Assert.NotNull(asset);
        string html = Encoding.UTF8.GetString(asset.Value.Bytes);
        Assert.Contains("<", html);
    }
}
