namespace Crawler.Core.Services;

public static class UrlCalculator
{
    public static string NormalizeUrl(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            throw new ArgumentException("URL is required.", nameof(rawUrl));
        }

        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("URL must be a valid absolute URI.", nameof(rawUrl));
        }

        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty,
            Port = uri.IsDefaultPort ? -1 : uri.Port
        };

        var normalized = builder.Uri.GetLeftPart(UriPartial.Authority) + uri.AbsolutePath.TrimEnd('/') + (uri.Query.Length > 0 ? uri.Query : string.Empty);

        return normalized.TrimEnd('/') == string.Empty ? builder.Uri.GetLeftPart(UriPartial.Authority) : normalized;
    }

    public static decimal CalculateDomainLinkRatio(int localLinks, int totalLinks)
    {
        if (totalLinks <= 0)
        {
            return 0m;
        }

        return Math.Round((decimal)localLinks / totalLinks, 6);
    }
}
