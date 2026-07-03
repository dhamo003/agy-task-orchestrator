using AntigravityTaskRunner.Configuration;
using AntigravityTaskRunner.Terminal.Sessions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AntigravityTaskRunner.Terminal.Detection;

/// <summary>
/// Switches the model by sending a CLI command (e.g. `/model {model}`) via stdin.
/// </summary>
public class CliModelSwitcher : IModelSwitcher
{
    private readonly ModelOptions _options;
    private readonly ILogger<CliModelSwitcher> _logger;

    public CliModelSwitcher(IOptions<RunnerOptions> options, ILogger<CliModelSwitcher> logger)
    {
        _options = options.Value.ModelConfig;
        _logger = logger;
    }

    public async Task<bool> SwitchModelAsync(ITerminalSession session, string targetModel, CancellationToken token = default)
    {
        _logger.LogInformation("Switching model to: {TargetModel}", targetModel);

        session.ClearOutputBuffers();
        
        // 1. Send /model to open the interactive model selection menu
        await session.SendInputAsync("/model", token);
        await Task.Delay(1000, token);

        // 2. Type the target model name to filter the interactive list
        await session.SendInputAsync(targetModel, token);
        await Task.Delay(500, token);

        // 3. The list is filtered, the first match should be selected. Send Enter to confirm.
        // SendInputAsync appends Environment.NewLine which simulates Enter.
        await session.SendInputAsync("", token);
        await Task.Delay(500, token);

        return true;
    }
}
