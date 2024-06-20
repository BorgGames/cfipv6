namespace cfipv6;

using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public sealed class AddressUpdater: IHostedService {
    readonly HttpClient zonesAPI;
    readonly ILogger<AddressUpdater> log;

    public AddressUpdater(string email, string apiKey, ILogger<AddressUpdater> log) {
        ArgumentException.ThrowIfNullOrEmpty(apiKey, nameof(apiKey));
        this.log = log ?? throw new ArgumentNullException(nameof(log));

        this.zonesAPI = new() {
            BaseAddress = new("https://api.cloudflare.com/client/v4/zones"),
            DefaultRequestHeaders = {
                Authorization = new("Bearer " + apiKey),
            },
        };
        this.zonesAPI.DefaultRequestHeaders.Add("X-Auth-Email", email);
    }

    async Task OnAddressChanged() {
        try {
            var address = IP.GetPublicStableIPv6();
            if (address is null) {
                this.log.LogWarning("No public IPv6 address found");
                return;
            }

            var response = await this.zonesAPI.PatchAsync(
                $"{ZONE_ID}/dns_records/{RECORD_ID}",
                new StringContent(JsonSerializer.Serialize(new {
                    type = "AAAA",
                    name = "example.com",
                    content = address.Address.ToString(),
                    ttl = 120,
                }), Encoding.UTF8, "application/json")
            ).ConfigureAwait(false);
        } catch (Exception e) {
            this.log.LogError(e, "Failed to update IP address: {Error}", e);
        }
    }

    async void AddressChangedHandler(object? _, EventArgs __)
    => await this.OnAddressChanged().ConfigureAwait(false);

    public async Task StartAsync(CancellationToken cancellationToken) {
        NetworkChange.NetworkAddressChanged += this.AddressChangedHandler;
        await this.OnAddressChanged().ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) {
        NetworkChange.NetworkAddressChanged -= this.AddressChangedHandler;
        return Task.CompletedTask;
    }
}
