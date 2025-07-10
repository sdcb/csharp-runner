using System.Net;

namespace Sdcb.CSharpRunner.Worker;

public class Register
{
    public static async Task LoginAsWorker(string registerHostUrl, string workerUrl, int maxRuns)
    {
        using HttpClient client = new();
        client.Timeout = TimeSpan.FromSeconds(100);
        int maxRetry = 3;
        for (int i = 0; i < maxRetry; i++)
        {
            try
            {
                Console.WriteLine($"({i + 1}/{maxRetry}) Attempting to register worker at {registerHostUrl} with service URL {workerUrl}, MaxRuns:{maxRuns}");
                // Attempt to register the worker
                HttpResponseMessage resp = await client.PostAsJsonAsync($"{registerHostUrl}/api/worker/register", new RegisterWorkerRequest
                {
                    WorkerUrl = workerUrl,
                    MaxRuns = maxRuns
                }, AppJsonContext.Default.RegisterWorkerRequest);
                resp.EnsureSuccessStatusCode();
                Console.WriteLine("Worker registered successfully.");
                return; // Exit if successful
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Failed to register worker: {ex.Message}");
                if (i == maxRetry - 1)
                {
                    throw; // Rethrow on last attempt
                }
                await Task.Delay(1000); // Wait before retrying
            }
        }
    }

    public static string GetServiceHttpUrl(ICollection<string> listeningUrls, string? exposedUrl)
    {
        if (!string.IsNullOrWhiteSpace(exposedUrl)) return exposedUrl;

        string myIp = GetMyNonLoopbackIP();
        int myPort = GetPortFromUrl(listeningUrls);
        return $"http://{myIp}:{myPort}";

        static string GetMyNonLoopbackIP()
        {
            string? myIp = Dns.GetHostEntry(Dns.GetHostName())
                .AddressList
                .FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                ?.ToString();
            if (string.IsNullOrEmpty(myIp))
            {
                throw new InvalidOperationException("No non-loopback IP address found.");
            }
            return myIp;
        }

        static int GetPortFromUrl(ICollection<string> listeningUrls)
        {
            foreach (string url in listeningUrls)
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
                {
                    if (uri.Scheme == Uri.UriSchemeHttp)
                    {
                        return uri.Port;
                    }
                }
            }

            throw new InvalidOperationException("No valid HTTP URL found in the listening URLs.");
        }
    }
}
