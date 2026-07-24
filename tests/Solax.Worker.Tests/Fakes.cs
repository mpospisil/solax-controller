using Solax.Core.Enums;
using Solax.Core.Interfaces;
using Solax.Core.Models;

namespace Solax.Worker.Tests;

/// <summary>Records applied settings and reflects them back as the new "current", like real hardware.</summary>
internal sealed class FakeEvChargerControl : IEvChargerControl
{
    public EvChargerSettings CurrentSettings { get; set; } = new(EvChargerMode.Stop, 0);

    public List<(EvChargerSettings Current, EvChargerSettings Target, string Reason)> Applied { get; } = [];

    /// <summary>How many times <see cref="ResetAsync"/> ran.</summary>
    public int ResetCount { get; private set; }

    public Task<EvChargerSettings> ReadSettingsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CurrentSettings);

    public Task<EvChargerSettings> ApplyAsync(
        EvChargerSettings current,
        EvChargerSettings target,
        string reason,
        CancellationToken cancellationToken = default)
    {
        Applied.Add((current, target, reason));
        CurrentSettings = target;
        return Task.FromResult(target);
    }

    /// <summary>Commands sent via <see cref="SendCommandAsync"/>, in order.</summary>
    public List<EvChargerControlCommand> Commands { get; } = [];

    public Task SendCommandAsync(EvChargerControlCommand command, string reason, CancellationToken cancellationToken = default)
    {
        Commands.Add(command);
        return Task.CompletedTask;
    }

    public Task ResetAsync(string reason, CancellationToken cancellationToken = default)
    {
        ResetCount++;
        Commands.Add(EvChargerControlCommand.StopCharging);
        CurrentSettings = new EvChargerSettings(EvChargerMode.Stop, 6);
        return Task.CompletedTask;
    }
}

/// <summary>Returns a canned decision and captures the last input the coordinator built.</summary>
internal sealed class StubChargingController : IChargingController
{
    public ChargingControlDecision NextDecision { get; set; } =
        new(ChargingControlAction.None, null, "stub");

    public ChargingControlInput? LastInput { get; private set; }

    public ChargingControlDecision Decide(ChargingControlInput input)
    {
        LastInput = input;
        return NextDecision;
    }
}
