namespace cfipv6;

using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

public sealed class AddressUpdater: IHostedService {
    readonly HttpClient zonesAPI;
    readonly AddressEntry[] entries;
    readonly ILogger<AddressUpdater> log;

    public AddressUpdater(IOptions<AddressUpdaterOptions> options, ILogger<AddressUpdater> log) {
        var opt = options?.Value ?? throw new ArgumentNullException(nameof(options));
        ArgumentException.ThrowIfNullOrEmpty(opt.Email);
        ArgumentException.ThrowIfNullOrEmpty(opt.ApiKey);
        this.log = log ?? throw new ArgumentNullException(nameof(log));

        this.zonesAPI = new() {
            BaseAddress = new("https://api.cloudflare.com/client/v4/zones/"),
            DefaultRequestHeaders = {
                Authorization = new("Bearer", opt.ApiKey),
            },
        };
        this.zonesAPI.DefaultRequestHeaders.Add("X-Auth-Email", opt.Email);
        this.entries = opt.Entries ?? throw new ArgumentNullException(nameof(opt.Entries));
        foreach (var entry in this.entries) {
            ArgumentException.ThrowIfNullOrEmpty(entry.ZoneID);
            ArgumentException.ThrowIfNullOrEmpty(entry.RecordID);
            ArgumentException.ThrowIfNullOrEmpty(entry.Domain);
        }
    }

    async Task OnAddressChanged() {
        UnicastIPAddressInformation? address;
        try {
            address = IP.GetPublicStableIPv6();
            if (address is null) {
                this.log.LogWarning("No public IPv6 address found");
                return;
            }
        } catch (Exception e) {
            this.log.LogError(e, "Failed to get IP address: {Error}", e);
            return;
        }

        foreach (var entry in this.entries) {
            string? responseContent = null;
            try {
                // TODO: check domain name
                var response = await this.zonesAPI.PatchAsync(
                    $"{entry.ZoneID}/dns_records/{entry.RecordID}",
                    new StringContent(JsonSerializer.Serialize(new {
                        type = "AAAA",
                        content = address.Address.ToString(),
                    }), Encoding.UTF8, "application/json")
                ).ConfigureAwait(false);

                if (response.Content is not null)
                    responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                this.log.LogInformation("Updated {Domain} to {Address}",
                                        entry.Domain, address.Address);
            } catch (Exception e) {
                this.log.LogError(e, "Failed to update {Domain} to {Address}: {Error} {Reason}",
                                     entry.Domain, address.Address, e, responseContent);
            }
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
