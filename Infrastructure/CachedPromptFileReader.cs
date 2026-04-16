using Microsoft.Extensions.Caching.Memory;


public sealed class CachedPromptFileReader(IMemoryCache memoryCache) : IPromptFileReader
{
    public string ReadAllText(string path)
    {
        return memoryCache.GetOrCreate(
            $"prompt:file:v1:{path}",
            entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
                if (!File.Exists(path))
                    throw new FileNotFoundException($"Prompt file not found: {path}", path);
                return File.ReadAllText(path);
            })!;
    }
}
