using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace PerformanceTests
{
    static class PerformanceMeasurement
    {
        const double ExecuteTime = 1.0;
        const double ExecuteProbeTime = 0.1;
        const int MaxNameLength = 30;
        const int MaxIterationCount = 1000000000;
        const int MinBucketCount = 1000;
        const int MaxBucketCount = 5000;

        static readonly Stopwatch sw = new();

        static PerformanceMeasurement()
        {
            sw.Start();
        }
        
        /// <summary>
        /// Measure execution time for a provided method
        /// </summary>
        /// <param name="action">A function to be tested</param>
        /// <param name="name">Display name</param>
        /// <param name="factor">Batch size (number of iterations inside function to be tested)</param>
        public static void Run(Action action, string name, int factor = 1)
        {
            // Warmup
            action();

            var approx = Probe(action);
            var iterCount = (int) Math.Min(ExecuteTime / approx, MaxIterationCount);

            if (iterCount <= MaxBucketCount)
            {
                var stat = new double[iterCount];

                // Console.WriteLine("Approx: {2}, bucket: {0}x{1}", iterCount, 1, approx);
                
                for (var i = 0; i < iterCount; i++)
                {
                    sw.Reset();
                    sw.Start();
                    action();
                    sw.Stop();
                    var t = sw.Elapsed.TotalSeconds;
                    stat[i] = t / factor;
                }

                PrintStatistics(name, stat);
            }
            else
            {
                var bucketSize = iterCount / MinBucketCount;
                var stat = new double[MinBucketCount];

                // Console.WriteLine("Approx: {2}, bucket: {0}x{1}", MinBucketCount, bucketSize, approx);
                
                for (var i = 0; i < MinBucketCount; i++)
                {
                    sw.Reset();
                    sw.Start();

                    for (var j = 0; j < bucketSize; j++)
                    {
                        action();
                    }

                    sw.Stop();

                    var t = sw.Elapsed.TotalSeconds;
                    stat[i] = t / (factor * bucketSize);
                }

                PrintStatistics(name, stat);
            }
        }

        /// <summary>
        /// Analyze and print statistics for execution time
        /// </summary>
        /// <param name="name">Display name</param>
        /// <param name="stat">Individual execution time</param>
        static void PrintStatistics(string name, double[] stat)
        {
            Array.Sort(stat);
            
            var average = stat.Average();
            var pc10 = stat[stat.Length / 10];

            Console.WriteLine("{0}: {1}s/op, {2}op/s | {3}s/op, {4}op/s",
                name.PadRight(MaxNameLength).Substring(0, MaxNameLength),
                DoubleToFixedString(pc10), DoubleToFixedString(1.0 / pc10),
                DoubleToFixedString(average), DoubleToFixedString(1.0 / average));
        }

        /// <summary>
        /// Convert floating point value to string of fixed width
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        static string DoubleToFixedString(double value)
        {
            var (postfix, v) = value switch
            {
                >= 1e12 or <= 1e-12 => throw new ArgumentOutOfRangeException(nameof(value), "Expected value in (1e-12, 1e12) range"),
                >= 1e9 => ('G', value * 1e-9),
                >= 1e6 => ('M', value * 1e-6),
                >= 1e3 => ('k', value * 1e-3),
                >= 1e0 => (' ', value),
                >= 1e-3 => ('m', value * 1e3),
                >= 1e-6 => ('u', value * 1e6),
                >= 1e-9 => ('n', value * 1e9),
                _ => ('p', value * 1e12)
            };

            return v.ToString("0.000", CultureInfo.InvariantCulture).Substring(0, 5) + ' ' + postfix;
        }        
        
        /// <summary>
        /// Roughly estimate execution time of a function
        /// </summary>
        /// <param name="action">Function to be tested</param>
        /// <returns>Approximate function execution time in seconds</returns>
        static double Probe(Action action)
        {
            sw.Reset();
            
            int count = 0, limit = 1;

            while (true)
            {
                sw.Start();
                
                for (; count < limit; ++count)
                {
                    action();
                }

                sw.Stop();

                var elapsed = sw.Elapsed.TotalSeconds;
                if (elapsed >= ExecuteProbeTime)
                    return elapsed / count;

                limit *= 2;
                
                // Overflow check
                if (limit < count)
                    return elapsed / count;
            }            
        }
    }
}