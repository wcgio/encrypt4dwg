using System.Net.Http;

namespace DwgTimedEncryptor.Windows.Services;

public sealed class NetworkTimeService
{
    private static readonly Uri[] TimeSources =
    [
        new("https://www.cloudflare.com/"),
        new("https://www.microsoft.com/"),
    ];

    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(3) };

    public async Task<(DateTime Now, string Source)> GetCurrentTimeAsync()
    {
        foreach (var source in TimeSources)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, source);
                request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
                using var response = await _httpClient.SendAsync(request);
                if (response.Headers.Date is { } date)
                {
                    return (date.LocalDateTime, "网络时间");
                }
            }
            catch (HttpRequestException)
            {
                // 尝试下一个可信 HTTPS 来源；全部失败后使用本机时间。
            }
            catch (TaskCanceledException)
            {
                // 网络超时后继续尝试下一个来源。
            }
        }

        return (DateTime.Now, "本机时间");
    }
}
