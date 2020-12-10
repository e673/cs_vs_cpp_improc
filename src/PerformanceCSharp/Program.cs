using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("PerformanceCSharp.Test")]

namespace PerformanceTests
{
    static unsafe class Program
    {
        const int size = 256;
        
        static void Main(string[] args)
        {
            PerformanceMeasurement.Run(() => { }, "Empty action");

            var img1 = new NativeImage<float>(size, size);
            var img2 = new NativeImage<float>(size, size);
            var kernel = new NativeImage<float>(7, 7);
            var res = new NativeImage<float>(size, size);

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

            PerformanceMeasurement.Run(() => ImageOperations.Sum_GetSetMethods(img1, img2, res), "Sum: naive, get/set");
            PerformanceMeasurement.Run(() => ImageOperations.Sum_RefMethod(img1, img2, res), "Sum: naive, ref pixel");
            PerformanceMeasurement.Run(() => ImageOperations.Sum_ThisProperty(img1, img2, res), "Sum: naive, this[,]");
            PerformanceMeasurement.Run(() => ImageOperations.Sum_Optimized(img1, img2, res), "Sum: optimized");
            PerformanceMeasurement.Run(() => ImageOperations.Sum_Avx(img1, img2, res), "Sum: avx");
            PerformanceMeasurement.Run(() => ImageOperations.Rotate180(img1, res), "Rotate: naive");
            PerformanceMeasurement.Run(() => ImageOperations.Rotate180_Optimized(img1, res), "Rotate: optimized");
            PerformanceMeasurement.Run(() => ImageOperations.Rotate180_Avx(img1, res), "Rotate: avx");
            PerformanceMeasurement.Run(() => ImageOperations.MedianFilter(img1, 1, res), "Median 3x3");
            PerformanceMeasurement.Run(() => ImageOperations.MedianFilter(img1, 2, res), "Median 5x5");
            PerformanceMeasurement.Run(() => ImageOperations.MedianFilter(img1, 3, res), "Median 7x7");
            PerformanceMeasurement.Run(() => ImageOperations.Convolve(img1, kernel, res), "Convolution: naive");
            PerformanceMeasurement.Run(() => ImageOperations.Convolve_Optimized(img1, kernel, res), "Convolution: optimized");
            PerformanceMeasurement.Run(() => ImageOperations.Convolve_Avx(img1, kernel, res), "Convolution: vector, avx");
            PerformanceMeasurement.Run(() => ImageOperations.Convolve_AvxIsolated(img1, kernel, res), "Convolution [*]: vector, avx");
        }
    }
}
