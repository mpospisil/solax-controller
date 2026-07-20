using System.Net.Sockets;
using NModbus;
using Solax.Core.Interfaces;
using Solax.Core.Models;

namespace Solax.Infrastructure.Modbus;

public sealed class ModbusTcpClient : IModbusClient
{
    // TcpClient.ConnectAsync has no built-in timeout: an unreachable or non-responding
    // device (wrong IP, firewalled, powered off) otherwise blocks the caller indefinitely.
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan IoTimeout = TimeSpan.FromSeconds(5);

    private readonly DeviceConfig _device;
    private TcpClient? _tcpClient;
    private IModbusMaster? _master;

    public ModbusTcpClient(DeviceConfig device)
    {
        _device = device;
    }

    public bool IsConnected => _tcpClient?.Connected ?? false;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(ConnectTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var tcpClient = new TcpClient();
        try
        {
            await tcpClient.ConnectAsync(_device.Host, _device.Port, linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            tcpClient.Dispose();
            throw new TimeoutException(
                $"Timed out connecting to Modbus device at {_device.Host}:{_device.Port} after {ConnectTimeout}.");
        }

        // Guards the NModbus read/write calls below, which don't accept a CancellationToken.
        tcpClient.ReceiveTimeout = (int)IoTimeout.TotalMilliseconds;
        tcpClient.SendTimeout = (int)IoTimeout.TotalMilliseconds;

        _tcpClient?.Dispose();
        _tcpClient = tcpClient;
        _master = new ModbusFactory().CreateMaster(tcpClient);
    }

    public async Task<ushort[]> ReadHoldingRegistersAsync(
        ushort startAddress,
        ushort numberOfPoints,
        CancellationToken cancellationToken = default)
    {
        if (_master is null || !IsConnected)
        {
            throw new InvalidOperationException("Modbus client is not connected. Call ConnectAsync first.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await _master.ReadHoldingRegistersAsync(_device.UnitId, startAddress, numberOfPoints).ConfigureAwait(false);
    }

    public async Task<ushort[]> ReadInputRegistersAsync(
        ushort startAddress,
        ushort numberOfPoints,
        CancellationToken cancellationToken = default)
    {
        if (_master is null || !IsConnected)
        {
            throw new InvalidOperationException("Modbus client is not connected. Call ConnectAsync first.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await _master.ReadInputRegistersAsync(_device.UnitId, startAddress, numberOfPoints).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        _master?.Dispose();
        _tcpClient?.Dispose();
        return ValueTask.CompletedTask;
    }
}
