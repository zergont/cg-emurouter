using CgEmulator.Config;
using CgEmulator.Mqtt;
using CgEmulator.Sim;

var emulatorConfig = ConfigLoader.Load(args);

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddSingleton(emulatorConfig);
builder.Services.AddSingleton<SimulationManager>();
builder.Services.AddSingleton<MqttPublisher>();
builder.Services.AddHostedService<SimulationWorker>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/state", (SimulationManager simulation) => Results.Ok(simulation.GetState()));

app.MapPost("/api/objects", (SimulationManager simulation, CreateObjectsRequest request) =>
{
    return Results.Ok(simulation.Recreate(request));
});

app.MapPost("/api/start", (SimulationManager simulation) =>
{
    simulation.Start();
    return Results.Ok(new { status = "RUN" });
});

app.MapPost("/api/stop", (SimulationManager simulation) =>
{
    simulation.Stop();
    return Results.Ok(new { status = "STOP" });
});

app.Urls.Clear();
app.Urls.Add($"http://{emulatorConfig.Web.BindIp}:{emulatorConfig.Web.Port}");

app.Run();
