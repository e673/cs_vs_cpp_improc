using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace PerformanceTests
{
    static unsafe class ImageOperations
    {
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void Sum_GetSetMethods(NativeImage<float> img1, NativeImage<float> img2, NativeImage<float> res)
        {
            for (var j = 0; j < res.Height; j++)
            for (var i = 0; i < res.Width; i++)
                res.SetUnsafe(i, j, img1.GetUnsafe(i, j) + img2.GetUnsafe(i, j));
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void Sum_RefMethod(NativeImage<float> img1, NativeImage<float> img2, NativeImage<float> res)
        {
            for (var j = 0; j < res.Height; j++)
            for (var i = 0; i < res.Width; i++)
                res.Pixel(i, j) = img1.Pixel(i, j) + img2.Pixel(i, j);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void Sum_ThisProperty(NativeImage<float> img1, NativeImage<float> img2, NativeImage<float> res)
        {
            for (var j = 0; j < res.Height; j++)
            for (var i = 0; i < res.Width; i++)
                res[i, j] = img1[i, j] + img2[i, j];
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void Sum_Optimized(NativeImage<float> img1, NativeImage<float> img2, NativeImage<float> res)
        {
            var w = res.Width;

            for (var j = 0; j < res.Height; j++)
            {
                var p1 = img1.PixelAddr(0, j);
                var p2 = img2.PixelAddr(0, j);
                var r = res.PixelAddr(0, j);

                for (var i = 0; i < w; i++)
                    r[i] = p1[i] + p2[i];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void Sum_Avx(NativeImage<float> img1, NativeImage<float> img2, NativeImage<float> res)
        {
            var w8 = res.Width / 8 * 8;

            for (var j = 0; j < res.Height; j++)
            {
                var p1 = img1.PixelAddr(0, j);
                var p2 = img2.PixelAddr(0, j);
                var r = res.PixelAddr(0, j);

                for (var i = 0; i < w8; i += 8)
                {
                    Avx.StoreAligned(r, Avx.Add(Avx.LoadAlignedVector256(p1), Avx.LoadAlignedVector256(p2)));

                    p1 += 8;
                    p2 += 8;
                    r += 8;
                }
                
                for (var i = w8; i < res.Width; i++)
                    *r++ = *p1++ + *p2++;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void Rotate180(NativeImage<float> src, NativeImage<float> dst)
        {
            var w = src.Width - 1;
            var h = src.Height - 1;

            for (var j = 0; j <= h; j++)
            for (var i = 0; i <= w; i++)
            {
                dst[w - i, h - j] = src[i, j];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void Rotate180_Avx(NativeImage<float> src, NativeImage<float> dst)
        {
            var w8 = src.Width / 8 * 8;

            for (var j = 0; j < src.Height; j++)
            {
                var s = src.PixelAddr(0, j);
                var d = dst.PixelAddr(src.Width, j);

                for (var i = 0; i < w8; i += 8)
                {
                    var v = Avx.Permute(Avx.LoadAlignedVector256(s), 0x1b);
                    s += 8;
                    d -= 8;
                    Avx.Store(d, Avx.Permute2x128(v, v, 1));
                }

                for (var i = w8; i < src.Width; i++)
                    *--d = *s++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void Rotate180_Optimized(NativeImage<float> src, NativeImage<float> dst)
        {
            var w = src.Width;

            for (var j = 0; j < src.Height; j++)
            {
                var s = src.PixelAddr(0, j);
                var d = dst.PixelAddr(src.Width, j);

                for (var i = 0; i < w; i++)
                    *--d = *s++;
            }
        }

        public static void MedianFilter(NativeImage<float> src, int rad, NativeImage<float> res)
        {
            new Median(src, rad, res).Perform();
        }

        public static void Convolve(NativeImage<float> img, NativeImage<float> kernel, NativeImage<float> res)
        {
            using var tmp = new NativeImage<float>(kernel.Width, kernel.Height);
            Rotate180(kernel, tmp);
            new Convolution(img, tmp, res).Perform();
        }

        public static void Convolve_Optimized(NativeImage<float> img, NativeImage<float> kernel, NativeImage<float> res)
        {
            using var tmp = new NativeImage<float>(kernel.Width, kernel.Height);
            Rotate180(kernel, tmp);
            new Convolution(img, tmp, res).PerformOptimized();
        }

        public static void Convolve_Avx(NativeImage<float> img, NativeImage<float> kernel, NativeImage<float> res)
        {
            using var tmp = new NativeImage<float>(kernel.Width, kernel.Height);
            Rotate180(kernel, tmp);
            new Convolution(img, tmp, res).PerformVector();
        }

        public static void Convolve_AvxIsolated(NativeImage<float> img, NativeImage<float> kernel, NativeImage<float> res)
        {
            using var tmp = new NativeImage<float>(kernel.Width, kernel.Height);
            Rotate180(kernel, tmp);
            new Convolution(img, tmp, res).PerformVectorIsolated();
        }
        
        sealed class Median
        {
            readonly NativeImage<float> src;
            readonly int rad;
            readonly NativeImage<float> res;
            readonly int diam, N;
            readonly float[] arr;
            readonly int w1, h1;

            public Median(NativeImage<float> src, int rad, NativeImage<float> res)
            {
                this.src = src;
                this.rad = rad;
                this.res = res;
                diam = 2 * rad + 1;
                N = diam * diam;
                arr = new float[N];
                w1 = src.Width - 1;
                h1 = src.Height - 1;
            }

            public void Perform()
            {
                for (var j = 0; j < rad; j++)
                for (var i = 0; i < src.Width; i++)
                    ProcessChecked(i, j);

                for (var j = rad; j < src.Height - rad; j++)
                {
                    for (var i = 0; i < rad; i++)
                        ProcessChecked(i, j);

                    for (var i = rad; i < src.Width - rad; i++)
                        ProcessUnchecked(i, j);

                    for (var i = src.Width - rad; i < src.Width; i++)
                        ProcessChecked(i, j);
                }
                
                for (var j = src.Height - rad; j < src.Height; j++)
                for (var i = 0; i < src.Width; i++)
                    ProcessChecked(i, j);
            }

            void ProcessChecked(int x0, int y0)
            {
                var k = 0;
                
                for (var j = 0; j < diam; j++)
                for (var i = 0; i < diam; i++)
                {
                    var x = Math.Max(0, Math.Min(w1, x0 - rad + i));
                    var y = Math.Max(0, Math.Min(h1, y0 - rad + j));
                    arr[k++] = src[x, y];
                }

                Array.Sort(arr);
                res[x0, y0] = arr[N / 2];
            }

            void ProcessUnchecked(int x0, int y0)
            {
                var k = 0;
                
                for (var j = 0; j < diam; j++)
                for (var i = 0; i < diam; i++)
                {
                    arr[k++] = src[x0 - rad + i, y0 - rad + j];
                }

                Array.Sort(arr);
                res[x0, y0] = arr[N / 2];
            }
        }
        
        sealed class Convolution
        {
            readonly NativeImage<float> img;
            readonly NativeImage<float> kernel;
            readonly NativeImage<float> res;
            readonly int ofsx, ofsy, kw1, kh1, iw1, ih1;

            public Convolution(NativeImage<float> img, NativeImage<float> kernel, NativeImage<float> res)
            {
                this.img = img;
                this.kernel = kernel;
                this.res = res;
                ofsx = kernel.Width / 2;
                ofsy = kernel.Height / 2;
                kw1 = kernel.Width - 1;
                kh1 = kernel.Height - 1;
                iw1 = img.Width - 1;
                ih1 = img.Height - 1;
            }

            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            public void Perform()
            {
                for (var j = 0; j < ofsy; j++)
                for (var i = 0; i < res.Width; i++)
                    ProcessChecked(i, j);

                for (var j = ofsy; j < res.Height + ofsy - kh1; j++)
                {
                    for (var i = 0; i < ofsx; i++)
                        ProcessChecked(i, j);

                    for (var i = ofsx; i < res.Width + ofsx - kw1; i++)
                        ProcessUnchecked(i, j);

                    for (var i = res.Width + ofsx - kw1; i < res.Width; i++)
                        ProcessChecked(i, j);
                }

                for (var j = res.Height + ofsy - kh1; j < res.Height; j++)
                for (var i = 0; i < res.Width; i++)
                    ProcessChecked(i, j);
            }
            
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            public void PerformOptimized()
            {
                for (var j = 0; j < ofsy; j++)
                for (var i = 0; i < res.Width; i++)
                    ProcessCheckedOptimized(i, j);

                for (var j = ofsy; j < res.Height + ofsy - kh1; j++)
                {
                    for (var i = 0; i < ofsx; i++)
                        ProcessCheckedOptimized(i, j);

                    for (var i = ofsx; i < res.Width + ofsx - kw1; i++)
                        ProcessUncheckedOptimized(i, j);

                    for (var i = res.Width + ofsx - kw1; i < res.Width; i++)
                        ProcessCheckedOptimized(i, j);
                }

                for (var j = res.Height + ofsy - kh1; j < res.Height; j++)
                for (var i = 0; i < res.Width; i++)
                    ProcessCheckedOptimized(i, j);
            }

            public void PerformVector()
            {
                var w8 = (res.Width - kw1) / 8 * 8;

                for (var j = 0; j < ofsy; j++)
                for (var i = 0; i < res.Width; i++)
                    ProcessCheckedOptimized(i, j);

                for (var j = ofsy; j < res.Height + ofsy - kh1; j++)
                {
                    for (var i = 0; i < ofsx; i++)
                        ProcessCheckedOptimized(i, j);

                    for (var i = 0; i < w8; i += 8)
                        ProcessUncheckedVector(i + ofsx, j);

                    for (var i = w8; i < res.Width - kw1; i++)
                        ProcessUncheckedOptimized(i + ofsx, j);

                    for (var i = res.Width + ofsx - kw1; i < res.Width; i++)
                        ProcessCheckedOptimized(i, j);
                }

                for (var j = res.Height + ofsy - kh1; j < res.Height; j++)
                for (var i = 0; i < res.Width; i++)
                    ProcessCheckedOptimized(i, j);
            }

            public void PerformVectorIsolated()
            {
                var w8 = (res.Width - kw1) / 8 * 8;

                for (var j = ofsy; j < res.Height + ofsy - kh1; j++)
                for (var i = 0; i < w8; i += 8)
                    ProcessUncheckedVector(i + ofsx, j);
            }

            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            void ProcessChecked(int x0, int y0)
            {
                var sum = 0.0f;

                for (var j = 0; j <= kh1; j++)
                {
                    var y = Math.Max(0, Math.Min(ih1, y0 - ofsy + j));

                    for (var i = 0; i <= kw1; i++)
                    {
                        var x = Math.Max(0, Math.Min(iw1, x0 - ofsx + i));
                        sum += img[x, y] * kernel[i, j];
                    }
                }

                res[x0, y0] = sum;
            }            

            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            void ProcessCheckedOptimized(int x0, int y0)
            {
                var sum = 0.0f;
                var x1 = Math.Max(0, ofsx - x0);
                var x2 = Math.Min(kw1, iw1 + ofsx - x0);

                for (var j = 0; j <= kh1; j++)
                {
                    var y = Math.Max(0, Math.Min(ih1, y0 - ofsy + j));
                    var p = img.PixelAddr(x0 - ofsx, y);
                    var k = kernel.PixelAddr(0, j);

                    for (var i = 0; i < x1; i++)
                        sum += p[x1] * k[i];

                    for (var i = x1; i <= x2; i++)
                        sum += p[i] * k[i];

                    for (var i = x2 + 1; i <= kw1; i++)
                        sum += p[x2] * k[i];
                }

                res[x0, y0] = sum;
            }

            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            void ProcessUnchecked(int x0, int y0)
            {
                var sum = 0.0f;

                for (var j = 0; j <= kh1; j++)
                {
                    var y = y0 - ofsy + j;
                    
                    for (var i = 0; i <= kw1; i++)
                        sum += img[x0 - ofsx + i, y] * kernel[i, j];
                }

                res[x0, y0] = sum;
            }
            
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            void ProcessUncheckedOptimized(int x0, int y0)
            {
                var sum = 0.0f;

                for (var j = 0; j <= kh1; j++)
                {
                    var p = img.PixelAddr(x0 - ofsx, y0 - ofsy + j);
                    var k = kernel.PixelAddr(0, j);

                    for (var i = 0; i <= kw1; i++)
                        sum += p[i] * k[i];
                }

                res[x0, y0] = sum;
            }

            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            void ProcessUncheckedVector(int x0, int y0)
            {
                var sum = Vector256<float>.Zero;

                for (var j = 0; j <= kh1; j++)
                {
                    var s = img.PixelAddr(x0 - ofsx, y0 - ofsy + j);
                    var k = kernel.PixelAddr(0, j);

                    for (var i = 0; i <= kw1; i++)
                    {
                        sum = Avx.Add(sum, Avx.Multiply(Avx.LoadVector256(s), Avx.BroadcastScalarToVector256(k)));
                        s++;
                        k++;
                    }
                }

                Avx.Store(res.PixelAddr(x0, y0), sum);
            }
        }

    }
}