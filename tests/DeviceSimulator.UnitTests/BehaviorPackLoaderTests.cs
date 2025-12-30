using DeviceSimulator.BehaviorRuntime;

namespace DeviceSimulator.UnitTests;

public class BehaviorPackLoaderTests
{
    private readonly BehaviorPackLoader _loader = new();

    [Fact]
    public async Task LoadAsync_WithValidPack_ReturnsSuccess()
    {
        // Arrange
        var packPath = GetSamplePackPath();

        // Act
        var result = await _loader.LoadAsync(packPath);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("SampleAstmDevice", result.Value.Manifest.Name);
        Assert.Equal("POC-Vendor", result.Value.Manifest.Vendor);
        Assert.Equal("ASTM-E1381", result.Value.Manifest.Protocol);
        Assert.Equal("1.0.0", result.Value.Manifest.Version);
    }

    [Fact]
    public async Task LoadAsync_WithValidPack_LoadsWorkflow()
    {
        // Arrange
        var packPath = GetSamplePackPath();

        // Act
        var result = await _loader.LoadAsync(packPath);

        // Assert
        Assert.True(result.IsSuccess);
        var workflow = result.Value!.Workflow;

        Assert.Equal("Idle", workflow.InitialState);
        Assert.Contains("Idle", workflow.States.Keys);
        Assert.Contains("Connected", workflow.States.Keys);
        Assert.Contains("Receiving", workflow.States.Keys);
        Assert.Contains("Transmitting", workflow.States.Keys);
    }

    [Fact]
    public async Task LoadAsync_WithValidPack_LoadsQuirks()
    {
        // Arrange
        var packPath = GetSamplePackPath();

        // Act
        var result = await _loader.LoadAsync(packPath);

        // Assert
        Assert.True(result.IsSuccess);
        var quirks = result.Value!.Quirks;

        Assert.Equal(50, quirks.Timing.ResponseDelayMs);
        Assert.Equal(30000, quirks.Timing.TimeoutMs);
        Assert.True(quirks.Behavior.SendCrLf);
    }

    [Fact]
    public async Task LoadAsync_WithValidPack_LoadsScenarios()
    {
        // Arrange
        var packPath = GetSamplePackPath();

        // Act
        var result = await _loader.LoadAsync(packPath);

        // Assert
        Assert.True(result.IsSuccess);
        var scenarios = result.Value!.Scenarios;

        Assert.NotEmpty(scenarios.Scenarios);
        Assert.Contains(scenarios.Scenarios, s => s.Name == "SimpleHandshake");
        Assert.Contains(scenarios.Scenarios, s => s.Name == "SingleResult");
    }

    [Fact]
    public async Task LoadAsync_WithInvalidPath_ReturnsFailure()
    {
        // Arrange
        var packPath = "/nonexistent/path";

        // Act
        var result = await _loader.LoadAsync(packPath);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetSamplePackPath()
    {
        // Find the behavior-packs directory relative to test execution
        var currentDir = Directory.GetCurrentDirectory();
        var searchPaths = new[]
        {
            Path.Combine(currentDir, "behavior-packs", "SampleAstmDevice"),
            Path.Combine(currentDir, "..", "..", "..", "..", "..", "behavior-packs", "SampleAstmDevice"),
            Path.Combine(currentDir, "..", "..", "..", "behavior-packs", "SampleAstmDevice"),
            "/workspace/behavior-packs/SampleAstmDevice"
        };

        foreach (var path in searchPaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }
        }

        // Return the expected path even if not found - test will fail with clear error
        return Path.Combine(currentDir, "behavior-packs", "SampleAstmDevice");
    }
}
