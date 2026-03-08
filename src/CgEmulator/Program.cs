using CgEmulator.Config;
using CgEmulator.Mqtt;
using CgEmulator.Sim;

var emulatorConfig = ConfigLoader.Load(args);

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddSingleton(emulatorConfig);
builder.Services.AddSingleton<SimulationManager>();
builder.Services.AddSingleton<ReplayBuffer>(sp =>
    new ReplayBuffer(emulatorConfig.Replay, sp.GetRequiredService<ILogger<ReplayBuffer>>()));
builder.Services.AddSingleton<MqttPublisher>();
builder.Services.AddHostedService<SimulationWorker>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/version", () => Results.Ok(new { version = "0.0.1" }));

app.MapGet("/api/state", (SimulationManager simulation) => Results.Ok(simulation.GetState()));

app.MapGet("/api/replay/status", (MqttPublisher publisher) => Results.Ok(publisher.GetReplayStatus()));

app.MapPost("/api/objects", (SimulationManager simulation, CreateObjectsRequest request) =>
{
    return Results.Ok(simulation.Recreate(request));
});

app.MapPost("/api/start", (SimulationManager simulation) =>
{
    simulation.Start();
    return Results.Ok(new { status = "\u0420\u0410\u0411\u041E\u0422\u0410\u0415\u0422" });
});

app.MapPost("/api/stop", (SimulationManager simulation) =>
{
    simulation.Stop();
    return Results.Ok(new { status = "\u041E\u0421\u0422\u0410\u041D\u041E\u0412\u041B\u0415\u041D\u041E" });
});

app.Urls.Clear();
app.Urls.Add($"http://{emulatorConfig.Web.BindIp}:{emulatorConfig.Web.Port}");

app.Run();
