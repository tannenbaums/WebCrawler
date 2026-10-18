using Crawler.Core.Services;

namespace Crawler.Tests;

public class UrlCalculatorTests
{
    [Theory]
    [InlineData(3, 4, 0.75)]
    [InlineData(0, 5, 0)]
    [InlineData(1, 3, 0.333333)]
    [InlineData(5, 0, 0)]
    public void CalculateDomainLinkRatio_ReturnsLocalLinksOverTotalLinks(int localLinks, int totalLinks, decimal expected)
    {
        var ratio = UrlCalculator.CalculateDomainLinkRatio(localLinks, totalLinks);

        Assert.Equal(expected, ratio);
    }

    [Theory]
    [InlineData("https://example.com/path/#section", "https://example.com/path")]
    [InlineData("https://example.com/", "https://example.com")]
    public void NormalizeUrl_RemovesFragmentAndTrailingSlash(string input, string expected)
    {
        var normalized = UrlCalculator.NormalizeUrl(input);

        Assert.Equal(expected, normalized);
    }
}