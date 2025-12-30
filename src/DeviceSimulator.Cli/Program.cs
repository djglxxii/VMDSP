using DeviceSimulator.Abstractions;
using DeviceSimulator.BehaviorRuntime;
using DeviceSimulator.Observability;

namespace DeviceSimulator.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Device Simulator CLI v1.0.0");
        Console.WriteLine("===========================\n");

        // Parse arguments
        var options = ParseArgs(args);

        if (options.ShowHelp)
        {
            ShowHelp();
            return 0;
        }

        if (string.IsNullOrEmpty(options.PackPath))
        {
            Console.WriteLine("Error: --packPath is required");
            ShowHelp();
            return 1;
        }

        // Load behavior pack
        Console.WriteLine($"Loading behavior pack from: {options.PackPath}");

        var loader = new BehaviorPackLoader();
        var result = await loader.LoadAsync(options.PackPath);

        if (!result.IsSuccess || result.Value == null)
        {
            Console.WriteLine($"Error loading pack: {result.ErrorMessage}");
            return 1;
        }

        var pack = result.Value;
        Console.WriteLine($"Loaded pack: {pack.Manifest.Name} v{pack.Manifest.Version}");
        Console.WriteLine($"  Protocol: {pack.Manifest.Protocol}");
        Console.WriteLine($"  Vendor: {pack.Manifest.Vendor}");
        Console.WriteLine($"  States: {pack.Workflow.States.Count}");
        Console.WriteLine($"  Scenarios: {pack.Scenarios.Scenarios.Count}");
        Console.WriteLine();

        // Create simulated devices
        var traceSink = new InMemoryTraceSink();

        Console.WriteLine($"Creating {options.Count} simulated device(s)...\n");

        var devices = new List<(string Id, IStateMachine StateMachine)>();

        for (int i = 0; i < options.Count; i++)
        {
            var deviceId = $"device-{i + 1:D3}";
            var stateMachine = new BehaviorStateMachine();
            stateMachine.Initialize(pack.Workflow);

            stateMachine.Transitioned += (sender, e) =>
            {
                Console.WriteLine($"[{deviceId}] {e.FromState} -> {e.ToState} (trigger: {e.Trigger})");
            };

            devices.Add((deviceId, stateMachine));
            Console.WriteLine($"  Created {deviceId} in state: {stateMachine.CurrentState}");
        }

        Console.WriteLine();
        Console.WriteLine("Simulator ready. Press Ctrl+C to exit.");
        Console.WriteLine("Commands: 'status', 'trigger <event>', 'quit'\n");

        // Simple command loop
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        while (!cts.Token.IsCancellationRequested)
        {
            Console.Write("> ");
            var input = Console.ReadLine();

            if (string.IsNullOrEmpty(input))
                continue;

            var parts = input.Split(' ', 2);
            var command = parts[0].ToLower();

            switch (command)
            {
                case "quit":
                case "exit":
                    cts.Cancel();
                    break;

                case "status":
                    foreach (var (id, sm) in devices)
                    {
                        Console.WriteLine($"  {id}: {sm.CurrentState}");
                    }
                    break;

                case "trigger":
                    if (parts.Length < 2)
                    {
                        Console.WriteLine("Usage: trigger <event> [payload]");
                        break;
                    }

                    var eventParts = parts[1].Split(' ', 2);
                    var eventName = eventParts[0];
                    var payload = eventParts.Length > 1 ? eventParts[1] : null;

                    foreach (var (id, sm) in devices)
                    {
                        var context = new StateMachineContext(
                            id,
                            $"session-{id}",
                            traceSink,
                            (data, ct) =>
                            {
                                var printable = System.Text.Encoding.UTF8.GetString(data)
                                    .Replace("\x05", "<ENQ>")
                                    .Replace("\x06", "<ACK>")
                                    .Replace("\x15", "<NAK>")
                                    .Replace("\x04", "<EOT>");
                                Console.WriteLine($"[{id}] SEND: {printable}");
                                return Task.CompletedTask;
                            });

                        var processResult = await sm.ProcessEventAsync(eventName, payload, context, cts.Token);

                        if (processResult.TransitionOccurred)
                        {
                            Console.WriteLine($"[{id}] Transition: {processResult.FromState} -> {processResult.ToState}");
                        }
                        else
                        {
                            Console.WriteLine($"[{id}] No transition for event '{eventName}'");
                        }
                    }
                    break;

                case "help":
                    Console.WriteLine("Commands:");
                    Console.WriteLine("  status  - Show device states");
                    Console.WriteLine("  trigger <event> [payload] - Trigger an event");
                    Console.WriteLine("  quit    - Exit the simulator");
                    break;

                default:
                    Console.WriteLine($"Unknown command: {command}. Type 'help' for commands.");
                    break;
            }
        }

        Console.WriteLine("\nShutting down...");
        return 0;
    }

    static CliOptions ParseArgs(string[] args)
    {
        var options = new CliOptions();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help":
                case "-h":
                    options.ShowHelp = true;
                    break;

                case "--packPath":
                case "-p":
                    if (i + 1 < args.Length)
                        options.PackPath = args[++i];
                    break;

                case "--count":
                case "-c":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var count))
                        options.Count = count;
                    break;

                case "--protocol":
                    if (i + 1 < args.Length)
                        options.Protocol = args[++i];
                    break;
            }
        }

        return options;
    }

    static void ShowHelp()
    {
        Console.WriteLine("Usage: DeviceSimulator.Cli [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --packPath, -p <path>   Path to behavior pack directory (required)");
        Console.WriteLine("  --count, -c <n>         Number of devices to simulate (default: 1)");
        Console.WriteLine("  --protocol <name>       Protocol filter (optional)");
        Console.WriteLine("  --help, -h              Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  DeviceSimulator.Cli --packPath ./behavior-packs/SampleAstmDevice --count 2");
    }
}

class CliOptions
{
    public bool ShowHelp { get; set; }
    public string? PackPath { get; set; }
    public int Count { get; set; } = 1;
    public string? Protocol { get; set; }
}
