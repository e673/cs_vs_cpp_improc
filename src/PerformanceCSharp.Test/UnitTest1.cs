using System.Collections.Generic;
using Xunit;

namespace PerformanceTests
{
    public class ImageProcessingTests
    {
        static bool BitmapEquals<T>(NativeImage<T> img1, NativeImage<T> img2)
            where T : unmanaged
        {
            if (img1.Width != img2.Width || img1.Height != img2.Height)
                return false;

            for (int j = 0; j < img1.Height; j++)
            for (int i = 0; i < img1.Width; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(img1[i, j], img2[i, j]))
                    return false;
            }

            return true;
        }

        static NativeImage<T> Shape<T>(int w, int h, params T[] data)
            where T : unmanaged
        {
            var res = new NativeImage<T>(w, h);
            
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                var k = j * w + i;
                res[i, j] = k < data.Length ? data[k] : default;
            }

            return res;
        }
        
        [Fact]
        public void TestSum()
        {
            var img1 = Shape(2, 3, 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f);
            var img2 = Shape(2, 3, 2.0f, 4.0f, 1.0f, 5.0f, 3.0f, 7.0f);
            var img3 = Shape(2, 3, 3.0f, 6.0f, 4.0f, 9.0f, 8.0f, 13.0f);
            var res = new NativeImage<float>(2, 3);

            ImageOperations.Sum_GetSetMethods(img1, img2, res);
            Assert.True(BitmapEquals(img3, res));

            ImageOperations.Sum_RefMethod(img1, img2, res);
            Assert.True(BitmapEquals(img3, res));

            ImageOperations.Sum_ThisProperty(img1, img2, res);
            Assert.True(BitmapEquals(img3, res));
            
            ImageOperations.Sum_Avx(img1, img2, res);
            Assert.True(BitmapEquals(img3, res));

            ImageOperations.Sum_Optimized(img1, img2, res);
            Assert.True(BitmapEquals(img3, res));
        }

        [Fact]
        public void TestConvolve()
        {
            var img = Shape(3, 4, 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f, 11.0f, 12.0f);
            var kernel = Shape(2, 3, 1.0f, 0.0f, 0.0f, 2.0f, 3.0f, 1.0f);
            var expected = Shape(3, 4, 10.0f, 14.0f, 21.0f, 19.0f, 23.0f, 30.0f, 40.0f, 44.0f, 51.0f, 58.0f, 62.0f, 69.0f);
            var res = new NativeImage<float>(3, 4);

            ImageOperations.Convolve(img, kernel, res);
            Assert.True(BitmapEquals(res, expected));

            ImageOperations.Convolve_Optimized(img, kernel, res);
            Assert.True(BitmapEquals(res, expected));

            ImageOperations.Convolve_Avx(img, kernel, res);
            Assert.True(BitmapEquals(res, expected));
        }
    }
}