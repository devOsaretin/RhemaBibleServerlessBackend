


public static class ApiResponse
{
    public static ApiResponse<T> Success<T>(T data, string message = "Request successful") =>
        ApiResponse<T>.SuccessResponse(data, message);

    public static ApiResponse<T> Success<T>(string message = "Request successful") =>
        ApiResponse<T>.SuccessMessageOnly(message);

    public static ApiResponse<T> Error<T>(string message) =>
        ApiResponse<T>.ErrorResponse(message);
}
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }

    public object? Meta { get; set; }

    public static ApiResponse<T> SuccessResponse(T? data, string message = "Request successful", object? meta = null) =>
        new() { Success = true, Data = data, Message = message, Meta = meta };

    public static ApiResponse<T> ErrorResponse(string message) =>
        new() { Success = false, Message = message };

    public static ApiResponse<T> SuccessMessageOnly(string message = "Request successful") =>
      new() { Success = true, Message = message };

    public static ApiResponse<List<TItem>> FromPagedResult<TItem>(PagedResult<TItem> paged, string message = "Request successful") =>
        new()
        {
            Success = true,
            Data = paged.Items.ToList(),
            Meta = new
            {
                paged.TotalItems,
                paged.PageNumber,
                paged.PageSize,
                paged.TotalPages
            },
            Message = message
        };
}
