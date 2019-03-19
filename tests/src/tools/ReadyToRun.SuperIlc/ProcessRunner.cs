// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class ProcessInfo
{
    /// <summary>
    /// 10 minutes should be plenty for a CPAOT / Crossgen compilation.
    /// </summary>
    public const int DefaultTimeout = 600 * 1000;

    public string ProcessPath;
    public string Arguments;
    public bool UseShellExecute;
    public string LogPath;
    public int TimeoutMilliseconds = DefaultTimeout;
    public int ExpectedExitCode;
    public object Data;

    public bool Finished;
    public bool Succeeded;
    public bool TimedOut;
    public int DurationMilliseconds;
    public int ExitCode;
}

public class ProcessRunner : IDisposable
{
    public const int StateIdle = 0;
    public const int StateRunning = 1;
    public const int StateFinishing = 2;

    public const int TimeoutExitCode = -103;

    private readonly ProcessInfo _processInfo;

    private readonly AutoResetEvent _processExitEvent;

    private readonly int _processIndex;

    private Process _process;

    private readonly Stopwatch _stopwatch;

    /// <summary>
    /// This is actually a boolean flag but we're using int to let us use CPU-native interlocked exchange.
    /// </summary>
    private int _state;

    private TextWriter _logWriter;

    private CancellationTokenSource _cancellationTokenSource;

    public ProcessRunner(ProcessInfo processInfo, int processIndex, AutoResetEvent processExitEvent)
    {
        _processInfo = processInfo;
        _processIndex = processIndex;
        _processExitEvent = processExitEvent;

        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        _cancellationTokenSource = cancellationTokenSource;

        _stopwatch = new Stopwatch();
        _stopwatch.Start();
        _state = StateIdle;

        _logWriter = new StreamWriter(_processInfo.LogPath);

        ProcessStartInfo psi = new ProcessStartInfo()
        {
            FileName = _processInfo.ProcessPath,
            Arguments = _processInfo.Arguments,
            UseShellExecute = _processInfo.UseShellExecute,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        _process = new Process();
        _process.StartInfo = psi;
        _process.EnableRaisingEvents = true;
        _process.Exited += new EventHandler(ExitEventHandler);

        Interlocked.Exchange(ref _state, StateRunning);

        _process.Start();

        _process.OutputDataReceived += new DataReceivedEventHandler(StandardOutputEventHandler);
        _process.BeginOutputReadLine();

        _process.ErrorDataReceived += new DataReceivedEventHandler(StandardErrorEventHandler);
        _process.BeginErrorReadLine();

        Task.Run(() =>
        {
            try
            {
                Task.Delay(_processInfo.TimeoutMilliseconds, cancellationTokenSource.Token).Wait();
                StopProcessAtomic();
            }
            catch (TaskCanceledException)
            {
                // Ignore cancellation
            }
        });
    }

    public void Dispose()
    {
        CleanupProcess();
    }

    private void CleanupProcess()
    {
        if (_cancellationTokenSource != null)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = null;
        }

        if (_process != null)
        {
            _process.Dispose();
            _process = null;
        }

        if (_logWriter != null)
        {
            _logWriter.Dispose();
            _logWriter = null;
        }
    }

    private void ExitEventHandler(object sender, EventArgs eventArgs)
    {
        StopProcessAtomic();
    }

    private void StopProcessAtomic()
    {
        if (Interlocked.CompareExchange(ref _state, StateFinishing, StateRunning) != StateRunning)
        {
            return;
        }

        _cancellationTokenSource.Cancel();

        bool success;
        if (_process.WaitForExit(0))
        {
            _processInfo.ExitCode = _process.ExitCode;
            success = (_processInfo.ExitCode == _processInfo.ExpectedExitCode);
            if (success)
            {
                Console.WriteLine(
                    $"{_processIndex}: succeeded in {_processInfo.DurationMilliseconds} msecs; " +
                    $"exit code {_processInfo.ExitCode}: {_processInfo.ProcessPath} {_processInfo.Arguments}");
                _processInfo.Succeeded = true;
            }
            else
            {
                Console.Error.WriteLine(
                    $"{_processIndex}: failed in {_processInfo.DurationMilliseconds} msecs; " +
                    $"exit code {_processInfo.ExitCode}, expected{_processInfo.ExpectedExitCode}: " +
                    $"{_processInfo.ProcessPath} {_processInfo.Arguments}");
            }
        }
        else
        {
            _process.Kill();
            _processInfo.ExitCode = TimeoutExitCode;
            _processInfo.TimedOut = true;
            success = false;
            Console.Error.WriteLine(
                $"{_processIndex}: timed out in {_processInfo.DurationMilliseconds} msecs: " +
                $"{_processInfo.ProcessPath} {_processInfo.Arguments}");
        }

        _processInfo.Finished = true;
        _processInfo.DurationMilliseconds = (int)_stopwatch.ElapsedMilliseconds;

        _logWriter.Close();

        CleanupProcess();

        Interlocked.Exchange(ref _state, StateIdle);
        _processExitEvent?.Set();
    }

    private void StandardOutputEventHandler(object sender, DataReceivedEventArgs eventArgs)
    {
        if (!string.IsNullOrEmpty(eventArgs.Data))
        {
            _logWriter.WriteLine(eventArgs.Data);
            Console.Out.WriteLine(_processIndex.ToString() + ": " + eventArgs.Data);
        }
    }

    private void StandardErrorEventHandler(object sender, DataReceivedEventArgs eventArgs)
    {
        if (!string.IsNullOrEmpty(eventArgs.Data))
        {
            _logWriter.WriteLine(eventArgs.Data);
            Console.Error.WriteLine(_processIndex.ToString() + ": " + eventArgs.Data);
        }
    }

    public bool IsAvailable()
    {
        return _state == StateIdle;
    }
}
