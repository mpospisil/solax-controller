using System.Net.Sockets;
using NModbus;
using Solax.Core.Interfaces;
using Solax.Core.Models;

namespace Solax.Infrastructure.Modbus;

public sealed class ModbusTcpClient : IModbusClient
{
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
        var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(_device.Host, _device.Port, cancellationToken).ConfigureAwait(false);

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

    public ValueTask DisposeAsync()
    {
        _master?.Dispose();
        _tcpClient?.Dispose();
        return ValueTask.CompletedTask;
    }
}
