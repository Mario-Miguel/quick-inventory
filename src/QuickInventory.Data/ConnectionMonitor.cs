using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using QuickInventory.Core.Services;

namespace QuickInventory.Data;

/// <summary>
/// Comprueba cada pocos segundos que se llega a la base de datos. Además, cualquier operación
/// que falle por la conexión (ver <see cref="ConnectionFailureInterceptor"/>) la marca al momento
/// como perdida, sin esperar a la siguiente comprobación.
/// </summary>
internal sealed class ConnectionMonitor(string connectionString) : IConnectionStatus, IAsyncDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim checking = new(1, 1);
    private readonly CancellationTokenSource stop = new();
    private Task? loop;

    public bool Connected { get; private set; } = true;

    public event Action? Changed;

    /// <summary>Empieza las comprobaciones periódicas. Se llama una vez al arrancar.</summary>
    public void Start() => loop ??= RunAsync(stop.Token);

    public async Task CheckAsync()
    {
        await checking.WaitAsync();
        try
        {
            using var timeout = new CancellationTokenSource(CheckTimeout);
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(timeout.Token);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(timeout.Token);
            SetConnected(true);
        }
        catch (Exception ex) when (ex is DbException or OperationCanceledException or TimeoutException)
        {
            SetConnected(false);
        }
        finally
        {
            checking.Release();
        }
    }

    /// <summary>Una operación ha fallado porque no se llega a la base de datos.</summary>
    public void ReportFailure() => SetConnected(false);

    private void SetConnected(bool connected)
    {
        if (Connected == connected) return;
        Connected = connected;
        Changed?.Invoke();
    }

    private async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Connected ? CheckInterval : RetryInterval, token);
                await CheckAsync();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync();
        if (loop is not null) await loop;
        stop.Dispose();
        checking.Dispose();
    }
}

/// <summary>
/// Avisa a <see cref="ConnectionMonitor"/> cuando una operación de EF falla por la conexión.
/// Los errores que devuelve el propio servidor (<see cref="PostgresException"/>: una restricción,
/// un permiso...) no cuentan: con ellos la conexión funciona.
/// </summary>
internal sealed class ConnectionFailureInterceptor(ConnectionMonitor monitor) : DbConnectionInterceptor, IDbCommandInterceptor
{
    public override void ConnectionFailed(DbConnection connection, ConnectionErrorEventData eventData) =>
        Report(eventData.Exception);

    public override Task ConnectionFailedAsync(DbConnection connection, ConnectionErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Report(eventData.Exception);
        return Task.CompletedTask;
    }

    public void CommandFailed(DbCommand command, CommandErrorEventData eventData) =>
        Report(eventData.Exception);

    public Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Report(eventData.Exception);
        return Task.CompletedTask;
    }

    private void Report(Exception exception)
    {
        if (exception is NpgsqlException and not PostgresException) monitor.ReportFailure();
    }
}
