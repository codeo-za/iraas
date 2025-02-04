using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace IRAAS.ImageProcessing;

public class Timer
{
    private readonly IAppSettings _appSettings;
    public IDictionary<string, string> Timings => _timings;
    private readonly Stopwatch _stopwatch;
    private readonly Dictionary<string, string> _timings;

    public Timer(IAppSettings appSettings)
    {
        _appSettings = appSettings;
        _stopwatch = new Stopwatch();
        _timings = new Dictionary<string, string>();
    }

    public async Task<T> Time<T>(
        string identifier,
        Func<Task<T>> exec)
    {
        StartStopwatch();
        var result = await exec();
        RecordTiming(identifier);
        return result;
    }

    public T Time<T>(
        string identifier,
        Func<T> exec)
    {
        StartStopwatch();
        var result = exec();
        RecordTiming(identifier);
        return result;
    }

    public void Time(
        string identifier,
        Action exec)
    {
        StartStopwatch();
        exec();
        RecordTiming(identifier);
    }

    private void StartStopwatch()
    {
        if (!_appSettings.Verbose)
        {
            return;
        }

        _stopwatch.Start();
    }

    private void RecordTiming(string identifier)
    {
        if (!_appSettings.Verbose)
        {
            return;
        }

        _stopwatch.Stop();
        _timings[identifier] = _stopwatch.ElapsedMilliseconds.ToString();
        _stopwatch.Reset();
    }
}