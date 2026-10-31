using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Soenneker.Utils.File.Abstract;
using Soenneker.Utils.Directory.Abstract;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Soenneker.Python.Util.Abstract;
using Soenneker.Atomics.ValueBools;
using Soenneker.Asyncs.Semaphores;
using Soenneker.Utils.Process.Abstract;
using Soenneker.Utils.Process.Dtos;

namespace Soenneker.Python.Util;

internal sealed class PythonSession : IPythonSession
{
    private readonly Channel<string> _output = Channel.CreateUnbounded<string>();
    private readonly Queue<string> _errors = new();
    private readonly AsyncSemaphore _gate = new(1);
    private readonly CancellationTokenSource _lifetime;
    private readonly CancellationToken _lifetimeToken;
    private readonly string _directory;
    private readonly IDirectoryUtil _directoryUtil;
    private readonly object _disposeLock = new();
    private Process _process = null!;
    private Task _completion = null!;
    private Task? _disposal;
    private ValueAtomicBool _disposed;

    private PythonSession(string directory, IDirectoryUtil directoryUtil, CancellationToken cancellationToken)
    {
        _directoryUtil = directoryUtil;
        _directory = directory;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _lifetimeToken = _lifetime.Token;
    }

    public static async ValueTask<IPythonSession> Start(IProcessUtil processUtil, IFileUtil fileUtil, IDirectoryUtil directoryUtil,
        string interpreter, string script, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interpreter);
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        cancellationToken.ThrowIfCancellationRequested();
        string directory = await directoryUtil.CreateTempDirectory(cancellationToken).ConfigureAwait(false);
        var session = new PythonSession(directory, directoryUtil, cancellationToken);
        string path = Path.Combine(directory, "script.py");
        try
        {
            await fileUtil.Write(path, script, log: false, cancellationToken: cancellationToken).ConfigureAwait(false);
            session._process = await processUtil.StartDetached(new ProcessStartDto
            {
                FileName = interpreter, ArgumentList = ["-u", path], RedirectStandardInput = true, Log = false,
                EnvironmentVariables = PythonUtil.PythonEnvironmentVariables(),
                OutputCallback = line => session._output.Writer.TryWrite(line),
                ErrorCallback = session.CaptureError
            }, session._lifetimeToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Could not start Python interpreter '{interpreter}'.");
            session._completion = session.ObserveExit();
            return session;
        }
        catch
        {
            session._lifetime.Dispose();
            await directoryUtil.DeleteIfExists(directory, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private async Task ObserveExit()
    {
        await _process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        string diagnostics;
        lock (_errors)
            diagnostics = string.Join(Environment.NewLine, _errors);
        _output.Writer.TryComplete(new InvalidOperationException($"Python session exited with code {_process.ExitCode}. {diagnostics}"));
    }

    public async ValueTask<string> Exchange(string input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Contains('\r') || input.Contains('\n'))
            throw new ArgumentException("Session input must contain a single line.", nameof(input));
        ObjectDisposedException.ThrowIf(_disposed.Value, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeToken);
        SemaphoreLease lease = await _gate.Acquire(linked.Token).ConfigureAwait(false);
        try
        {
            await _process.StandardInput.WriteLineAsync(input.AsMemory(), linked.Token).ConfigureAwait(false);
            await _process.StandardInput.FlushAsync(linked.Token).ConfigureAwait(false);
            return await _output.Reader.ReadAsync(linked.Token).ConfigureAwait(false);
        }
        catch (ChannelClosedException error)
        {
            throw new InvalidOperationException($"Python session ended before responding. {error.InnerException?.Message}", error.InnerException ?? error);
        }
        catch
        {
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            lease.Dispose();
        }
    }

    private void CaptureError(string line)
    {
        lock (_errors)
        {
            if (_errors.Count == 40)
                _errors.Dequeue();
            _errors.Enqueue(line.Length > 2000 ? line[..2000] : line);
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposeLock)
        {
            if (_disposal is null)
            {
                _disposed.Value = true;
                _disposal = Close();
            }
            return new ValueTask(_disposal);
        }
    }

    private async Task Close()
    {
        await _lifetime.CancelAsync().ConfigureAwait(false);
        await _completion.ConfigureAwait(false);
        _process.Dispose();
        await _directoryUtil.DeleteIfExists(_directory, CancellationToken.None).ConfigureAwait(false);
        _lifetime.Dispose();
    }
}
