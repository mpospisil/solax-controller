using Solax.Core.Enums;
using Solax.Core.Interfaces;
using Solax.Core.Models;

namespace Solax.Worker.Tests;

/// <summary>Records applied settings and reflects them back as the new "current", like real hardware.</summary>
internal sealed class FakeEvChargerControl : IEvChargerControl
{
    private EvChargerSettings? _original;

    public EvChargerSettings CurrentSettings { get; set; } = new(EvChargerMode.Stop, 0);

    public List<(EvChargerSettings Current, EvChargerSettings Target, string Reason)> Applied { get; } = [];

    /// <summary>The settings restored by <see cref="RestoreOriginalAsync"/>, if it ran.</summary>
    public EvChargerSettings? Restored { get; private set; }

    public bool HasOriginal => _original is not null;

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

    public Task CaptureOriginalAsync(CancellationToken cancellationToken = default)
    {
        _original ??= CurrentSettings;
        return Task.CompletedTask;
    }

    public Task<bool> RestoreOriginalAsync(string reason, CancellationToken cancellationToken = default)
    {
        if (_original is null)
        {
            return Task.FromResult(false);
        }

        Restored = _original;
        CurrentSettings = _original;
        _original = null;
        return Task.FromResult(true);
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
