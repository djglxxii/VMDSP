using DeviceSimulator.Abstractions;
using DeviceSimulator.BehaviorRuntime;
using DeviceSimulator.Host;
using DeviceSimulator.Observability;

var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddSingleton<ITraceSink>(new InMemoryTraceSink());
builder.Services.AddSingleton<IBehaviorPackProvider, BehaviorPackLoader>();
builder.Services.AddSingleton<ISimulatorOrchestrator, SimulatorOrchestrator>();

var app = builder.Build();

// GET /packs - List loaded behavior packs
app.MapGet("/packs", (ISimulatorOrchestrator orchestrator) =>
{
    var packs = orchestrator.GetLoadedPacks()
        .Select(p => new PackInfoDto(
            p.Name,
            p.Vendor,
            p.Protocol,
            p.Version,
            p.Description,
            p.Supports
        ))
        .ToList();

    return Results.Ok(new ApiResponse<List<PackInfoDto>>(true, packs, null));
});

// POST /packs/load?path= - Load a behavior pack
app.MapPost("/packs/load", async (string path, ISimulatorOrchestrator orchestrator, CancellationToken ct) =>
{
    var result = await orchestrator.LoadPackAsync(path, ct);

    if (!result.IsSuccess)
    {
        return Results.BadRequest(new ApiResponse<PackInfoDto>(false, null, result.ErrorMessage));
    }

    var manifest = result.Value!;
    var dto = new PackInfoDto(
        manifest.Name,
        manifest.Vendor,
        manifest.Protocol,
        manifest.Version,
        manifest.Description,
        manifest.Supports
    );

    return Results.Ok(new ApiResponse<PackInfoDto>(true, dto, null));
});

// GET /devices - List active device groups
app.MapGet("/devices", (ISimulatorOrchestrator orchestrator) =>
{
    var groups = orchestrator.GetActiveGroups()
        .Select(g => new DeviceGroupDto(
            g.GroupId,
            g.PackName,
            g.DeviceCount,
            g.StartedAt,
            g.Status.ToString()
        ))
        .ToList();

    return Results.Ok(new ApiResponse<List<DeviceGroupDto>>(true, groups, null));
});

// POST /devices/start - Start simulated devices
app.MapPost("/devices/start", async (StartDevicesRequest request, ISimulatorOrchestrator orchestrator, CancellationToken ct) =>
{
    var result = await orchestrator.StartDevicesAsync(request.PackName, request.Count, ct);

    if (!result.IsSuccess)
    {
        return Results.BadRequest(new ApiResponse<DeviceGroupDto>(false, null, result.ErrorMessage));
    }

    var group = result.Value!;
    var dto = new DeviceGroupDto(
        group.GroupId,
        group.PackName,
        group.DeviceCount,
        group.StartedAt,
        group.Status.ToString()
    );

    return Results.Ok(new ApiResponse<DeviceGroupDto>(true, dto, null));
});

// POST /devices/stop - Stop simulated devices
app.MapPost("/devices/stop", async (StopDevicesRequest request, ISimulatorOrchestrator orchestrator, CancellationToken ct) =>
{
    var result = await orchestrator.StopDevicesAsync(request.GroupId, ct);

    if (!result.IsSuccess)
    {
        return Results.BadRequest(new ApiResponse<string>(false, null, result.ErrorMessage));
    }

    return Results.Ok(new ApiResponse<string>(true, "Stopped", null));
});

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();

// Make Program class accessible for testing
public partial class Program { }
