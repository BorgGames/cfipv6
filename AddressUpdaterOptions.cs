namespace cfipv6;

public class AddressUpdaterOptions {
    public required string Email { get; init; }
    public required string ApiKey { get; init; }
    public required AddressEntry[] Entries { get; init; }
}
