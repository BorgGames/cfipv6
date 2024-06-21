namespace cfipv6;

using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

public sealed class AddressUpdater: IHostedService {
    readonly HttpClient zonesAPI;
    readonly string zoneID;
    readonly string recordID;
    readonly string domain;
    readonly ILogger<AddressUpdater> log;

    public AddressUpdater(IOptions<AddressUpdaterOptions> options, ILogger<AddressUpdater> log) {
        var opt = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.log = log ?? throw new ArgumentNullException(nameof(log));

        this.zonesAPI = new() {
            BaseAddress = new("https://api.cloudflare.com/client/v4/zones/"),
            DefaultRequestHeaders = {
                Authorization = new("Bearer", opt.ApiKey),
            },
        };
        this.zonesAPI.DefaultRequestHeaders.Add("X-Auth-Email", opt.Email);
        this.zoneID = opt.ZoneID ?? throw new ArgumentNullException(nameof(opt.ZoneID));
        this.recordID = opt.RecordID ?? throw new ArgumentNullException(nameof(opt.RecordID));
        this.domain = opt.Domain ?? throw new ArgumentNullException(nameof(opt.Domain));
    }

    async Task OnAddressChanged() {
        string? responseContent = null;
        try {
            var address = IP.GetPublicStableIPv6();
            if (address is null) {
                this.log.LogWarning("No public IPv6 address found");
                return;
            }

            // TODO: check domain name

            var response = await this.zonesAPI.PatchAsync(
                $"{this.zoneID}/dns_records/{this.recordID}",
                new StringContent(JsonSerializer.Serialize(new {
                    type = "AAAA",
                    content = address.Address.ToString(),
                }), Encoding.UTF8, "application/json")
            ).ConfigureAwait(false);

            if (response.Content is not null)
                responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            this.log.LogInformation("Updated {Domain} to {Address}", this.domain, address.Address);
        } catch (Exception e) {
            this.log.LogError(e, "Failed to update IP address: {Error} {Reason}", e, responseContent);
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
