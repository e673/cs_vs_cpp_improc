using System;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

[assembly: InternalsVisibleTo("PerformanceCSharp.Test")]

namespace PerformanceTests
{
    public class Benchmarks
    {
        readonly NativeImage<float> img1, img2, kernel, res;
        
        const int size = 256;
        public Benchmarks()
        {
            img1 = new NativeImage<float>(size, size);
            img2 = new NativeImage<float>(size, size);
            kernel = new NativeImage<float>(7, 7);
            res = new NativeImage<float>(size, size);
            
            for (var j = 0; j < res.Height; j++)
            for (var i = 0; i < res.Width; i++)
            {
                img1[i, j] = 1.0f;
                img2[i, j] = 2.0f;
            }

            for (var j = 0; j < kernel.Height; j++)
            for (var i = 0; i < kernel.Width; i++)
            {
                kernel[i, j] = 1.0f;
            }
        }

        [Benchmark] public void Sum_GetSetMethods() => ImageOperations.Sum_GetSetMethods(img1, img2, res);
        [Benchmark] public void Sum_RefMethod() => ImageOperations.Sum_RefMethod(img1, img2, res);
        [Benchmark] public void Sum_ThisProperty() => ImageOperations.Sum_ThisProperty(img1, img2, res);
        [Benchmark] public void Sum_Optimized() => ImageOperations.Sum_Optimized(img1, img2, res);
        [Benchmark] public void Sum_Avx() => ImageOperations.Sum_Avx(img1, img2, res);
        [Benchmark] public void Rotate180() => ImageOperations.Rotate180(img1, res);
        [Benchmark] public void Rotate180_Optimized() => ImageOperations.Rotate180_Optimized(img1, res);
        [Benchmark] public void Rotate180_Avx() => ImageOperations.Rotate180_Avx(img1, res);
        [Benchmark] public void MedianFilter3x3() => ImageOperations.MedianFilter(img1, 1, res);
        [Benchmark] public void MedianFilter5x5() => ImageOperations.MedianFilter(img1, 2, res);
        [Benchmark] public void MedianFilter7x7() => ImageOperations.MedianFilter(img1, 3, res);
        [Benchmark] public void Convolve() => ImageOperations.Convolve(img1, kernel, res);
        [Benchmark] public void Convolve_Optimized() => ImageOperations.Convolve_Optimized(img1, kernel, res);
        [Benchmark] public void Convolve_Avx() => ImageOperations.Convolve_Avx(img1, kernel, res);
        [Benchmark] public void Convolve_AvxIsolated() => ImageOperations.Convolve_AvxIsolated(img1, kernel, res);
    }

    static class Program
    {
        static void Main(string[] args)
        {
            Console.Write("Use BenchmarkDotNet? [y/n]: ");
            if (Console.ReadLine() is "y" or "Y" or "yes")
            {
                BenchmarkRunner.Run<Benchmarks>();
            }
            else
            {
                var benchmarks = new Benchmarks();

                PerformanceMeasurement.Run(benchmarks.Sum_GetSetMethods, "Sum: naive, get/set");
                PerformanceMeasurement.Run(benchmarks.Sum_RefMethod, "Sum: naive, ref pixel");
                PerformanceMeasurement.Run(benchmarks.Sum_ThisProperty, "Sum: naive, this[,]");
                PerformanceMeasurement.Run(benchmarks.Sum_Optimized, "Sum: optimized");
                PerformanceMeasurement.Run(benchmarks.Sum_Avx, "Sum: avx");
                PerformanceMeasurement.Run(benchmarks.Rotate180, "Rotate: naive");
                PerformanceMeasurement.Run(benchmarks.Rotate180_Optimized, "Rotate: optimized");
                PerformanceMeasurement.Run(benchmarks.Rotate180_Avx, "Rotate: avx");
                PerformanceMeasurement.Run(benchmarks.MedianFilter3x3, "Median 3x3");
                PerformanceMeasurement.Run(benchmarks.MedianFilter5x5, "Median 5x5");
                PerformanceMeasurement.Run(benchmarks.MedianFilter7x7, "Median 7x7");
                PerformanceMeasurement.Run(benchmarks.Convolve, "Convolution: naive");
                PerformanceMeasurement.Run(benchmarks.Convolve_Optimized, "Convolution: optimized");
                PerformanceMeasurement.Run(benchmarks.Convolve_Avx, "Convolution: vector, avx");
                PerformanceMeasurement.Run(benchmarks.Convolve_AvxIsolated, "Convolution [*]: vector, avx");
            }
        }
    }
}
