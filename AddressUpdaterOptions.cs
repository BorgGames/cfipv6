namespace cfipv6;

public class AddressUpdaterOptions {
    public required string Email { get; init; }
    public required string ApiKey { get; init; }
    public required string ZoneID { get; init; }
    public required string RecordID { get; init; }
    public required string Domain { get; init; }
}
