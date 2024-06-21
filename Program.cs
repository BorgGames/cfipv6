using cfipv6;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<AddressUpdaterOptions>(builder.Configuration.GetSection("AddressUpdater"));
builder.Services.AddHostedService<AddressUpdater>();

var host = builder.Build();
host.Run();
