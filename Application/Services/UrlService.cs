public class UrlService(IConfiguration configuration) : IUrlService
{
    private readonly string _environment = configuration["Environment"]!;
    private readonly string _cdnUrlHost = configuration["CdnUrlHost"]!;


    private bool IsProd()
    {
        return _environment == "Prod";
    }

    public string ToCdn(string originalUrl)
    {
        if (!IsProd()) return originalUrl;

        var url = new Uri(originalUrl);
        return new UriBuilder(url)
        {
            Scheme = "https",
            Host = _cdnUrlHost,
            Port = -1

        }.ToString();
    }
}
