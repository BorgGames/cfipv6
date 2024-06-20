using cfipv6;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<AddressUpdater>();

var host = builder.Build();
host.Run();
