using System.IO;
using System.Text.Json;

namespace Delivergate.Desktop.Configuration;

public sealed class AppSettings
{
    public string ApiBaseUrl { get; init; } = "";

    public static AppSettings Load()
    {
        // Resolve beside the executable so launching from another folder also works.
        string path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));

        if (settings is null ||
            !Uri.TryCreate(settings.ApiBaseUrl, UriKind.Absolute, out Uri? address) ||
            (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrEmpty(address.Host) ||
            !string.IsNullOrEmpty(address.UserInfo) ||
            !string.IsNullOrEmpty(address.Query) ||
            !string.IsNullOrEmpty(address.Fragment))
        {
            throw new InvalidDataException("ApiBaseUrl must be an HTTP or HTTPS server address.");
        }

        return new AppSettings { ApiBaseUrl = address.AbsoluteUri.TrimEnd('/') + "/" };
    }
}
