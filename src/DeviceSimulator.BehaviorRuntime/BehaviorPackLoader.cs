using DeviceSimulator.Abstractions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DeviceSimulator.BehaviorRuntime;

/// <summary>
/// Loads behavior packs from disk.
/// </summary>
public class BehaviorPackLoader : IBehaviorPackProvider
{
    private readonly IDeserializer _deserializer;

    public BehaviorPackLoader()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public async Task<Result<BehaviorPack>> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return Result.Failure<BehaviorPack>($"Directory not found: {path}", ErrorCodes.NotFound);
            }

            var manifest = await LoadYamlFileAsync<BehaviorPackManifest>(
                Path.Combine(path, "manifest.yaml"), cancellationToken);

            if (manifest == null)
            {
                return Result.Failure<BehaviorPack>("manifest.yaml not found or invalid", ErrorCodes.PackLoadFailed);
            }

            manifest = manifest with { BasePath = path };

            var protocol = await LoadYamlFileAsync<ProtocolDefinition>(
                Path.Combine(path, "protocol.yaml"), cancellationToken)
                ?? new ProtocolDefinition { Type = "unknown" };

            var workflow = await LoadYamlFileAsync<WorkflowDefinition>(
                Path.Combine(path, "workflow.yaml"), cancellationToken)
                ?? new WorkflowDefinition { InitialState = "Idle" };

            var quirks = await LoadYamlFileAsync<QuirksDefinition>(
                Path.Combine(path, "quirks.yaml"), cancellationToken)
                ?? new QuirksDefinition();

            var scenarios = await LoadYamlFileAsync<ScenariosDefinition>(
                Path.Combine(path, "scenarios.yaml"), cancellationToken)
                ?? new ScenariosDefinition();

            var pack = new BehaviorPack(manifest, protocol, workflow, quirks, scenarios);

            var validationResult = ValidatePack(pack);
            if (!validationResult.IsSuccess)
            {
                return Result.Failure<BehaviorPack>(validationResult.ErrorMessage ?? "Validation failed", ErrorCodes.ValidationFailed);
            }

            return Result.Success(pack);
        }
        catch (Exception ex)
        {
            return Result.Failure<BehaviorPack>($"Failed to load behavior pack: {ex.Message}", ErrorCodes.PackLoadFailed);
        }
    }

    public Result ValidatePack(BehaviorPack pack)
    {
        if (string.IsNullOrWhiteSpace(pack.Manifest.Name))
        {
            return Result.Failure("Manifest name is required", ErrorCodes.ValidationFailed);
        }

        if (string.IsNullOrWhiteSpace(pack.Manifest.Protocol))
        {
            return Result.Failure("Manifest protocol is required", ErrorCodes.ValidationFailed);
        }

        if (string.IsNullOrWhiteSpace(pack.Workflow.InitialState))
        {
            return Result.Failure("Workflow initial state is required", ErrorCodes.ValidationFailed);
        }

        if (pack.Workflow.States.Count > 0 && !pack.Workflow.States.ContainsKey(pack.Workflow.InitialState))
        {
            return Result.Failure($"Initial state '{pack.Workflow.InitialState}' not found in states", ErrorCodes.ValidationFailed);
        }

        return Result.Success();
    }

    private async Task<T?> LoadYamlFileAsync<T>(string filePath, CancellationToken cancellationToken)
        where T : class
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        return _deserializer.Deserialize<T>(content);
    }
}
