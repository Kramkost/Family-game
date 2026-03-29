// =============================================================================
// FILE 1: TerrainDebugLogger.cs
// Wraps Unity Debug.Log with context tagging, timing, and validation reporting.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ProceduralTerrain.Debugging
{
    /// <summary>
    /// Severity levels for terrain generation events.
    /// </summary>
    public enum LogLevel { Verbose, Info, Warning, Error, Critical }

    /// <summary>
    /// Immutable record of a single logged generation event.
    /// </summary>
    public readonly struct GenerationLogEntry
    {
        public readonly DateTime Timestamp;
        public readonly LogLevel Level;
        public readonly string System;
        public readonly string Message;
        public readonly double ElapsedMs;

        public GenerationLogEntry(LogLevel level, string system, string message, double elapsedMs = 0)
        {
            Timestamp  = DateTime.UtcNow;
            Level      = level;
            System     = system;
            Message    = message;
            ElapsedMs  = elapsedMs;
        }

        public override string ToString() =>
            $"[{Timestamp:HH:mm:ss.fff}] [{Level}] [{System}] {Message}" +
            (ElapsedMs > 0 ? $"  ({ElapsedMs:F2} ms)" : string.Empty);
    }

    // -------------------------------------------------------------------------
    // Interface — keeps the rest of the system decoupled from the concrete logger
    // -------------------------------------------------------------------------
    public interface ITerrainLogger
    {
        void Log(LogLevel level, string system, string message, double elapsedMs = 0);
        void LogInfo(string system, string message);
        void LogWarning(string system, string message);
        void LogError(string system, string message);
        IReadOnlyList<GenerationLogEntry> GetHistory();
        StopwatchScope BeginTimed(string system, string label);
    }

    // -------------------------------------------------------------------------
    // Disposable scope — use with "using" to auto-log a timed block
    // -------------------------------------------------------------------------
    public sealed class StopwatchScope : IDisposable
    {
        private readonly ITerrainLogger _logger;
        private readonly Stopwatch      _sw;
        private readonly string         _system;
        private readonly string         _label;
        private          bool           _disposed;

        public StopwatchScope(ITerrainLogger logger, string system, string label)
        {
            _logger = logger;
            _system = system;
            _label  = label;
            _sw     = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _sw.Stop();
            _logger.Log(LogLevel.Info, _system, $"✔  {_label} completed.", _sw.Elapsed.TotalMilliseconds);
        }
    }

    // -------------------------------------------------------------------------
    // Concrete logger
    // -------------------------------------------------------------------------
    public sealed class TerrainDebugLogger : ITerrainLogger
    {
        private static TerrainDebugLogger _instance;
        public  static TerrainDebugLogger Instance => _instance ??= new TerrainDebugLogger();

        private readonly List<GenerationLogEntry> _history = new(1024);
        private          LogLevel                 _minimumLevel = LogLevel.Verbose;

        /// <summary>Set this to suppress verbose noise in shipping builds.</summary>
        public void SetMinimumLevel(LogLevel level) => _minimumLevel = level;

        // ---- ITerrainLogger -------------------------------------------------

        public void Log(LogLevel level, string system, string message, double elapsedMs = 0)
        {
            if (level < _minimumLevel) return;

            var entry = new GenerationLogEntry(level, system, message, elapsedMs);
            _history.Add(entry);

            string formatted = entry.ToString();
            switch (level)
            {
                case LogLevel.Verbose:
                case LogLevel.Info:    Debug.Log(formatted);        break;
                case LogLevel.Warning: Debug.LogWarning(formatted); break;
                case LogLevel.Error:
                case LogLevel.Critical:Debug.LogError(formatted);   break;
            }
        }

        public void LogInfo(string system, string message)    => Log(LogLevel.Info,    system, message);
        public void LogWarning(string system, string message) => Log(LogLevel.Warning, system, message);
        public void LogError(string system, string message)   => Log(LogLevel.Error,   system, message);

        public IReadOnlyList<GenerationLogEntry> GetHistory() => _history.AsReadOnly();

        public StopwatchScope BeginTimed(string system, string label)
        {
            Log(LogLevel.Info, system, $"▶  {label} started...");
            return new StopwatchScope(this, system, label);
        }

        // ---- Validation helpers ----------------------------------------------

        /// <summary>Assert a condition and log ERROR if it fails. Returns false on failure.</summary>
        public bool Validate(bool condition, string system, string failMessage)
        {
            if (condition) return true;
            LogError(system, $"VALIDATION FAILED: {failMessage}");
            return false;
        }

        /// <summary>Dump the last N log entries to a single string (useful for crash reports).</summary>
        public string DumpHistory(int lastN = 50)
        {
            int start = Math.Max(0, _history.Count - lastN);
            var sb = new System.Text.StringBuilder();
            for (int i = start; i < _history.Count; i++)
                sb.AppendLine(_history[i].ToString());
            return sb.ToString();
        }
    }
}
