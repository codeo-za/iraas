using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace IRAAS.ImageProcessing
{
    public class Timer
    {
        public IDictionary<string, string> Timings => _timings;
        private readonly Stopwatch _stopwatch;
        private readonly Dictionary<string, string> _timings;

        public Timer()
        {
            _stopwatch = new Stopwatch();
            _timings = new Dictionary<string, string>();
        }

        public async Task<T> Time<T>(
            string identifier,
            Func<Task<T>> exec)
        {
            _stopwatch.Start();
            var result = await exec();
            RecordTiming(identifier);
            return result;
        }

        public T Time<T>(
            string identifier,
            Func<T> exec)
        {
            _stopwatch.Start();
            var result = exec();
            RecordTiming(identifier);
            return result;
        }

        public void Time(
            string identifier,
            Action exec)
        {
            _stopwatch.Start();
            exec();
            RecordTiming(identifier);
        }

        private void RecordTiming(string identifier)
        {
            _stopwatch.Stop();
            _timings[identifier] = _stopwatch.ElapsedMilliseconds.ToString();
            _stopwatch.Reset();
        }
    }
}