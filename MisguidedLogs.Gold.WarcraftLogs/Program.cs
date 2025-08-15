using BunnyCDN.Net.Storage;
using MisguidedLogs.Gold.WarcraftLogs;
using MisguidedLogs.Gold.WarcraftLogs.Bunnycdn;
using MisguidedLogs.Gold.WarcraftLogs.Mappers;
using MisguidedLogs.Gold.WarcraftLogs.Mappers.Achivements;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseConsoleLifetime();

builder.Services.Configure<ClientConfig>(builder.Configuration);
var config = builder.Configuration.GetSection("ClientEndpoint").Get<ClientConfig>();

ArgumentNullException.ThrowIfNull(config);

builder.Services.AddSingleton(config);


builder.Services.AddSingleton<BunnyCdnServiceStorageUploader>(x => new BunnyCdnServiceStorageUploader(new BunnyCDNStorage(config.BunnyCdnStorage, config.BunnyAccessKey, "se")));
builder.Services.AddSingleton<BunnyCdnClientStorageUploader>(x => new BunnyCdnClientStorageUploader(new BunnyCDNStorage(config.BunnyClientStorage, config.BunnyClientAccessKey, "se")));
builder.Services.AddSingleton<BunnyCdnStorageLoader>(x => new BunnyCdnStorageLoader(new BunnyCDNStorage(config.BunnyCdnStorage, config.BunnyAccessKey, "se")));
builder.Services.AddTransient<Probability>();
builder.Services.AddTransient<SearchIndexPlayers>();
builder.Services.AddTransient<AchivementsMapper>();
builder.Services.AddTransient<AllAchivements>();

builder.Services.AddHostedService<Runner>();

//Get HostapplicationBuilder, needed to bypass generivWebHostService
var field = builder.GetType().GetField("_hostApplicationBuilder", BindingFlags.Instance | BindingFlags.NonPublic);
ArgumentNullException.ThrowIfNull(field);
var hostApplicatiobuilder = (HostApplicationBuilder?)field.GetValue(builder);
ArgumentNullException.ThrowIfNull(hostApplicatiobuilder);
//Continue after here

var builtApplication = hostApplicatiobuilder.Build();
HostingAbstractionsHostExtensions.Run(builtApplication);
