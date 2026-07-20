namespace Solax.Core.Interfaces;

public interface IModbusClient : IAsyncDisposable
{
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task<ushort[]> ReadHoldingRegistersAsync(
        ushort startAddress,
        ushort numberOfPoints,
        CancellationToken cancellationToken = default);

    Task<ushort[]> ReadInputRegistersAsync(
        ushort startAddress,
        ushort numberOfPoints,
        CancellationToken cancellationToken = default);
}
