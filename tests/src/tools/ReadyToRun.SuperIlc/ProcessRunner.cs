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

using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;

public class ProcessInfo
{
    /// <summary>
    /// 10 minutes should be plenty for a CPAOT / Crossgen compilation.
    /// </summary>
    public const int DefaultIlcTimeout = 600 * 1000;

    /// <summary>
    /// Test execution timeout.
    /// </summary>
    public const int DefaultExeTimeout = 200 * 1000;

    /// <summary>
    /// Test execution timeout under GC stress mode.
    /// </summary>
    public const int DefaultExeTimeoutGCStress = 2000 * 1000;

    public string ProcessPath;
    public string Arguments;
    public Dictionary<string, string> EnvironmentOverrides = new Dictionary<string, string>();
    public bool UseShellExecute;
    public string LogPath;
    public int TimeoutMilliseconds;
    public int ExpectedExitCode;
    public string InputFileName;
    public string OutputFileName;
    public long CompilationCostHeuristic;
    public bool CollectJittedMethods;
    public IEnumerable<string> MonitorModules;
    public IEnumerable<string> MonitorFolders;

    public bool Finished;
    public bool Succeeded;
    public bool TimedOut;
    public int DurationMilliseconds;
    public int ExitCode;
    public Dictionary<string, HashSet<string>> JittedMethods;
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

    private ReadyToRunJittedMethods _jittedMethods;

    private readonly Stopwatch _stopwatch;

    /// <summary>
    /// This is actually a boolean flag but we're using int to let us use CPU-native interlocked exchange.
    /// </summary>
    private int _state;

    private TextWriter _logWriter;

    private CancellationTokenSource _cancellationTokenSource;

    public ProcessRunner(ProcessInfo processInfo, int processIndex, ReadyToRunJittedMethods jittedMethods, AutoResetEvent processExitEvent)
    {
        _processInfo = processInfo;
        _processIndex = processIndex;
        _jittedMethods = jittedMethods;
        _processExitEvent = processExitEvent;

        _cancellationTokenSource = new CancellationTokenSource();

        _stopwatch = new Stopwatch();
        _stopwatch.Start();
        _state = StateIdle;

        _logWriter = new StreamWriter(_processInfo.LogPath);

        if (_processInfo.ProcessPath.Contains(' '))
        {
            _logWriter.Write($"\"{_processInfo.ProcessPath}\"");
        }
        else
        {
            _logWriter.Write(_processInfo.ProcessPath);
        }
        _logWriter.Write(' ');
        _logWriter.WriteLine(_processInfo.Arguments);
        _logWriter.WriteLine("<<<<");

        ProcessStartInfo psi = new ProcessStartInfo()
        {
            FileName = _processInfo.ProcessPath,
            Arguments = _processInfo.Arguments,
            UseShellExecute = _processInfo.UseShellExecute,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (KeyValuePair<string, string> environmentOverride in _processInfo.EnvironmentOverrides)
        {
            psi.EnvironmentVariables[environmentOverride.Key] = environmentOverride.Value;
        }

        _process = new Process();
        _process.StartInfo = psi;
        _process.EnableRaisingEvents = true;
        _process.Exited += new EventHandler(ExitEventHandler);

        Interlocked.Exchange(ref _state, StateRunning);

        _process.Start();
        if (_processInfo.CollectJittedMethods)
        {
            _jittedMethods.AddProcessMapping(_processInfo, _process);
        }

        _process.OutputDataReceived += new DataReceivedEventHandler(StandardOutputEventHandler);
        _process.BeginOutputReadLine();

        _process.ErrorDataReceived += new DataReceivedEventHandler(StandardErrorEventHandler);
        _process.BeginErrorReadLine();

        Task.Run(TimeoutWatchdog);
    }

    public void Dispose()
    {
        CleanupProcess();
        CleanupLogWriter();
    }

    private void TimeoutWatchdog()
    {
        try
        {
            CancellationTokenSource source = _cancellationTokenSource;
            if (source != null)
            {
                Task.Delay(_processInfo.TimeoutMilliseconds, source.Token).Wait();
                StopProcessAtomic();
            }
        }
        catch (TaskCanceledException)
        {
            // Ignore cancellation
        }
    }

    private void CleanupProcess()
    {
        if (_cancellationTokenSource != null)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = null;
        }

        // In ETW collection mode, the disposal is carried out in ReadyToRunJittedMethods
        // as we need to keep the process alive for the entire lifetime of the trace event
        // session, otherwise PID's may get recycled and we couldn't reliably back-translate
        // them into the logical process executions.
        if (_process != null && !_processInfo.CollectJittedMethods)
        {
            _process.Dispose();
            _process = null;
        }
    }

    private void CleanupLogWriter()
    {
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
        if (Interlocked.CompareExchange(ref _state, StateFinishing, StateRunning) == StateRunning)
        {
            _cancellationTokenSource.Cancel();
            _processInfo.DurationMilliseconds = (int)_stopwatch.ElapsedMilliseconds;

            _processExitEvent?.Set();
        }
    }

    private void StandardOutputEventHandler(object sender, DataReceivedEventArgs eventArgs)
    {
        if (!string.IsNullOrEmpty(eventArgs.Data))
        {
            _logWriter.WriteLine(eventArgs.Data);
        }
    }

    private void StandardErrorEventHandler(object sender, DataReceivedEventArgs eventArgs)
    {
        if (!string.IsNullOrEmpty(eventArgs.Data))
        {
            _logWriter.WriteLine(eventArgs.Data);
        }
    }

    public bool IsAvailable()
    {
        if (_state != StateFinishing)
        {
            return _state == StateIdle;
        }

        if (_process.WaitForExit(0))
        {
            _process.WaitForExit();
            _processInfo.ExitCode = _process.ExitCode;
            _processInfo.Succeeded = (_processInfo.ExitCode == _processInfo.ExpectedExitCode);
            _logWriter.WriteLine(">>>>");
            if (_processInfo.Succeeded)
            {
                _logWriter.WriteLine(
                    $"Succeeded in {_processInfo.DurationMilliseconds} msecs, " +
                    $"exit code {_processInfo.ExitCode} = 0x{_processInfo.ExitCode:X8}");
                Console.WriteLine(
                    $"{_processIndex}: succeeded in {_processInfo.DurationMilliseconds} msecs; " +
                    $"exit code {_processInfo.ExitCode} = 0x{_processInfo.ExitCode:X8}: " +
                    $"{_processInfo.ProcessPath} {_processInfo.Arguments}");
                _processInfo.Succeeded = true;
            }
            else
            {
                _logWriter.WriteLine(
                    $"Failed in {_processInfo.DurationMilliseconds} msecs, " +
                    $"exit code {_processInfo.ExitCode} = 0x{_processInfo.ExitCode:X8}, " +
                    $"expected {_processInfo.ExpectedExitCode} = 0x{_processInfo.ExpectedExitCode:X8}");
                Console.Error.WriteLine(
                    $"{_processIndex}: failed in {_processInfo.DurationMilliseconds} msecs; " +
                    $"exit code {_processInfo.ExitCode} = 0x{_processInfo.ExitCode:X8}, " +
                    $"expected {_processInfo.ExpectedExitCode} = 0x{_processInfo.ExpectedExitCode:X8}: " +
                    $"{_processInfo.ProcessPath} {_processInfo.Arguments}");
            }
        }
        else
        {
            _process.Kill();
            _process.WaitForExit();
            _processInfo.ExitCode = TimeoutExitCode;
            _processInfo.TimedOut = true;
            _processInfo.Succeeded = false;
            _logWriter.WriteLine(">>>>");
            _logWriter.WriteLine($"Timed out in {_processInfo.DurationMilliseconds} msecs");
            Console.Error.WriteLine(
                $"{_processIndex}: timed out in {_processInfo.DurationMilliseconds} msecs: " +
                $"{_processInfo.ProcessPath} {_processInfo.Arguments}");
        }

        CleanupProcess();

        _processInfo.Finished = true;

        _logWriter.Flush();
        _logWriter.Close();

        CleanupLogWriter();

        _state = StateIdle;
        return true;
    }
}
