

using Microsoft.Azure.Functions.Worker.Http;

public static class HttpRequestExtensions
{
    public static (int pageNumber, int pageSize) GetPagination(this HttpRequestData req)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var pageSizeString = query["pageSize"];
        var pageNumberString = query["pageNumber"];

        var pageNumber = GetInt(pageNumberString, 1);
        var pageSize = GetInt(pageSizeString, 20);

        pageSize = Math.Clamp(pageSize, 1, 100);
        pageNumber = Math.Max(1, pageNumber);

        return (pageNumber, pageSize);
    }

    private static int GetInt(string? value, int defaultValue) =>
    int.TryParse(value, out var result) ? result : defaultValue;
}