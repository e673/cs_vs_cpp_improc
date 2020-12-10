#include <cstdio>
#include <immintrin.h>
#include <algorithm>
#include <vector>
#include "image.hpp"
#include "perf.hpp"

const int size = 256;

template <typename T>
void Init(Image<T> &image, T value)
{
    for (int j = 0; j < image.Height(); j++)
    for (int i = 0; i < image.Width(); i++)
        image(i, j) = value;
}

void ImageSum(const Image<float> &img1, const Image<float> &img2, Image<float> &res)
{
    for (int j = 0; j < res.Height(); j++)
    for (int i = 0; i < res.Width(); i++)
        res(i, j) = img1(i, j) + img2(i, j);
}

void ImageSumOptimized(const Image<float> &img1, const Image<float> &img2, Image<float> &res)
{
    for (int j = 0; j < res.Height(); j++)
    {
        auto p1 = &img1(0, j);
        auto p2 = &img2(0, j);
        auto p = &res(0, j);
        
        for (int i = 0; i < res.Width(); i++)
            p[i] = p1[i] + p2[i];
    }
}

void ImageSumUsingAvx(const Image<float> &img1, const Image<float> &img2, Image<float> &res)
{
    int w8 = res.Width() / 8 * 8;

    for (int j = 0; j < res.Height(); j++)
    {
        auto p1 = &img1(0, j);
        auto p2 = &img2(0, j);
        auto r = &res(0, j);

        for (int i = 0; i < w8; i += 8)
        {
            _mm256_store_ps(r, _mm256_add_ps(_mm256_load_ps(p1), _mm256_load_ps(p2)));

            p1 += 8;
            p2 += 8;
            r += 8;
        }

        for (int i = w8; i < res.Width(); i++)
            *r++ = *p1++ + *p2++;
    }
}

void Rotate180(const Image<float> &src, Image<float> &dst)
{
    int w = src.Width() - 1;
    int h = src.Height() - 1;

    for (int j = 0; j <= h; j++)
    for (int i = 0; i <= w; i++)
    {
        dst(w - i, h - j) = src(i, j);
    }
}

void Rotate180Optimized(const Image<float> &src, Image<float> &dst)
{
    for (int j = 0; j <= src.Height(); j++)
    {
        auto s = &src(0, j);
        auto d = &dst(src.Width(), j);
        
        for (int i = 0; i < src.Width(); i++)
            *--d = *s++;        
    }
}

void Rotate180UsingAvx(const Image<float> &src, Image<float> &dst)
{
    int w8 = src.Width() / 8 * 8;

    for (int j = 0; j < src.Height(); j++)
    {
        auto s = &src(0, j);
        auto d = &dst(src.Width(), j);

        for (int i = 0; i < w8; i += 8)
        {
            auto v = _mm256_permute_ps(_mm256_load_ps(s), 0x1b);
            s += 8;
            d -= 8;
            _mm256_store_ps(d, _mm256_permute2f128_ps(v, v, 1));
        }

        for (int i = w8; i < src.Width(); i++)
            *--d = *s++;
    }
}


class Convolution
{
private:
    const Image<float> &img;
    const Image<float> &kernel;
    Image<float> &res;
    const int ofsx, ofsy, kw1, kh1, iw1, ih1;

public:    
    Convolution(const Image<float> &img, const Image<float> &kernel, Image<float> &res);
    void Perform();
    void PerformOptimized();
    void PerformVector();
    void PerformVectorIsolated();
    
private:
    
    void ProcessChecked(int x, int y);
    void ProcessCheckedOptimized(int x, int y);
    void ProcessUnchecked(int x, int y);
    void ProcessUncheckedOptimized(int x, int y);
    void ProcessUncheckedVector(int x, int y);
};

Convolution::Convolution(const Image<float> &img, const Image<float> &kernel, Image<float> &res)
    : img(img), kernel(kernel), res(res)
    , ofsx(kernel.Width() / 2), ofsy(kernel.Height() / 2)
    , kw1(kernel.Width() - 1), kh1(kernel.Height() - 1)
    , iw1(img.Width() - 1), ih1(img.Height() - 1)
{ }

void Convolution::Perform()
{
    for (int j = 0; j < ofsy; j++)
    for (int i = 0; i < res.Width(); i++)
        ProcessChecked(i, j);

    for (int j = ofsy; j < res.Height() + ofsy - kh1; j++)
    {
        for (int i = 0; i < ofsx; i++)
            ProcessChecked(i, j);

        for (int i = ofsx; i < res.Width() + ofsx - kw1; i++)
            ProcessUnchecked(i, j);

        for (int i = res.Width() + ofsx - kw1; i < res.Width(); i++)
            ProcessChecked(i, j);
    }

    for (int j = res.Height() + ofsy - kh1; j < res.Height(); j++)
    for (int i = 0; i < res.Width(); i++)
        ProcessChecked(i, j);
}

void Convolution::PerformOptimized()
{
    for (int j = 0; j < ofsy; j++)
    for (int i = 0; i < res.Width(); i++)
        ProcessCheckedOptimized(i, j);

    for (int j = ofsy; j < res.Height() + ofsy - kh1; j++)
    {
        for (int i = 0; i < ofsx; i++)
            ProcessCheckedOptimized(i, j);

        for (int i = ofsx; i < res.Width() + ofsx - kw1; i++)
            ProcessUncheckedOptimized(i, j);

        for (int i = res.Width() + ofsx - kw1; i < res.Width(); i++)
            ProcessCheckedOptimized(i, j);
    }

    for (int j = res.Height() + ofsy - kh1; j < res.Height(); j++)
    for (int i = 0; i < res.Width(); i++)
        ProcessCheckedOptimized(i, j);
}

void Convolution::PerformVector()
{
    int w8 = (res.Width() - kw1) / 8 * 8;
    
    for (int j = 0; j < ofsy; j++)
    for (int i = 0; i < res.Width(); i++)
        ProcessCheckedOptimized(i, j);

    for (int j = ofsy; j < res.Height() + ofsy - kh1; j++)
    {
        for (int i = 0; i < ofsx; i++)
            ProcessCheckedOptimized(i, j);

        for (int i = 0; i < w8; i += 8)
            ProcessUncheckedVector(i + ofsx, j);

        for (int i = w8; i < res.Width() - kw1; i++)
            ProcessUncheckedOptimized(i + ofsx, j);        
        
        for (int i = res.Width() + ofsx - kw1; i < res.Width(); i++)
            ProcessCheckedOptimized(i, j);
    }

    for (int j = res.Height() + ofsy - kh1; j < res.Height(); j++)
    for (int i = 0; i < res.Width(); i++)
        ProcessCheckedOptimized(i, j);
}

void Convolution::PerformVectorIsolated()
{
    int w8 = (res.Width() - kw1) / 8 * 8;
    
    for (int j = ofsy; j < res.Height() + ofsy - kh1; j++)
        for (int i = 0; i < w8; i += 8)
            ProcessUncheckedVector(i + ofsx, j);
}

void Convolution::ProcessChecked(int x0, int y0)
{    
    float sum = 0.0f;

    for (int j = 0; j <= kh1; j++)
    {
        int y = std::max(0, std::min(ih1, y0 - ofsy + j));
        
        for (int i = 0; i <= kw1; i++)
        {
            int x = std::max(0, std::min(iw1, x0 - ofsx + i));
            sum += img(x, y) * kernel(i, j);
        }
    }

    res(x0, y0) = sum;
}

void Convolution::ProcessCheckedOptimized(int x0, int y0)
{    
    float sum = 0.0f;
    int x1 = std::max(0, ofsx - x0);
    int x2 = std::min(kw1, iw1 + ofsx - x0);

    for (int j = 0; j <= kh1; j++)
    {
        int y = std::max(0, std::min(ih1, y0 - ofsy + j));
        auto p = &img(x0 - ofsx, y);
        auto k = &kernel(0, j);        
        
        for (int i = 0; i < x1; i++)
            sum += p[x1] * k[i];

        for (int i = x1; i <= x2; i++)
            sum += p[i] * k[i];

        for (int i = x2 + 1; i <= kw1; i++)
            sum += p[x2] * k[i];        
    }

    res(x0, y0) = sum;
}

void Convolution::ProcessUnchecked(int x0, int y0)
{
    float sum = 0.0f;

    for (int j = 0; j <= kh1; j++)
    {
        int y = y0 - ofsy + j;
        
        for (int i = 0; i <= kw1; i++)
        {
            sum += img(x0 - ofsx + i, y) * kernel(i, j);
        }
    }

    res(x0, y0) = sum;
}

void Convolution::ProcessUncheckedOptimized(int x0, int y0)
{
    float sum = 0.0f;

    for (int j = 0; j <= kh1; j++)
    {
        auto p = &img(x0 - ofsx, y0 - ofsy + j);
        auto k = &kernel(0, j);
        
        for (int i = 0; i <= kw1; i++)
            sum += p[i] * k[i];
    }

    res(x0, y0) = sum;
}

void Convolution::ProcessUncheckedVector(int x0, int y0)
{
    auto sum = _mm256_setzero_ps();

    for (int j = 0; j <= kh1; j++)
    {
        auto s = &img(x0 - ofsx, y0 - ofsy + j);
        auto k = &kernel(0, j);

        for (int i = 0; i <= kw1; i++)
        {
            sum = _mm256_add_ps(sum, _mm256_mul_ps(_mm256_loadu_ps(s), _mm256_broadcast_ss(k)));
            s++;
            k++;
        }
    }
    
    _mm256_storeu_ps(&res(x0, y0), sum);
}

void ImageConvolution(const Image<float> &img, const Image<float> &kernel, Image<float> &res)
{
    Image<float> tmp(kernel.Width(), kernel.Height());
    Rotate180(kernel, tmp);
    Convolution(img, tmp, res).Perform();
}

void ImageConvolutionOptimized(const Image<float> &img, const Image<float> &kernel, Image<float> &res)
{
    Image<float> tmp(kernel.Width(), kernel.Height());
    Rotate180(kernel, tmp);
    Convolution(img, tmp, res).PerformOptimized();
}

void ImageConvolutionUsingAvx(const Image<float> &img, const Image<float> &kernel, Image<float> &res)
{
    Image<float> tmp(kernel.Width(), kernel.Height());
    Rotate180(kernel, tmp);
    Convolution(img, tmp, res).PerformVector();
}

void ImageConvolutionUsingAvxIsolated(const Image<float> &img, const Image<float> &kernel, Image<float> &res)
{
    Image<float> tmp(kernel.Width(), kernel.Height());
    Rotate180(kernel, tmp);
    Convolution(img, tmp, res).PerformVectorIsolated();
}

class Median
{
private:
    const Image<float> &src;
    Image<float> &res;
    const int rad, diam, N, w1, h1;
    std::vector<float> arr;

public:    
    Median(const Image<float> &src, int rad, Image<float> &res);
    void Perform();
    
private:
    
    void ProcessChecked(int x, int y);
    void ProcessUnchecked(int x, int y);
};

Median::Median(const Image<float> &src, int rad, Image<float> &res)
    : src(src), rad(rad), res(res), diam(2 * rad + 1), N(diam * diam), arr(N), w1(src.Width() - 1), h1(src.Height() - 1) { }

void Median::Perform()
{
    for (int j = 0; j < rad; j++)
    for (int i = 0; i < src.Width(); i++)
        ProcessChecked(i, j);

    for (int j = rad; j < src.Height() - rad; j++)
    {
        for (int i = 0; i < rad; i++)
            ProcessChecked(i, j);

        for (int i = rad; i < src.Width() - rad; i++)
            ProcessUnchecked(i, j);

        for (int i = src.Width() - rad; i < src.Width(); i++)
            ProcessChecked(i, j);
    }
    
    for (int j = src.Height() - rad; j < src.Height(); j++)
    for (int i = 0; i < src.Width(); i++)
        ProcessChecked(i, j);
}

void Median::ProcessChecked(int x0, int y0)
{
    int k = 0;
    
    for (int j = 0; j < diam; j++)
    for (int i = 0; i < diam; i++)
    {
        int x = std::max(0, std::min(w1, x0 - rad + i));
        int y = std::max(0, std::min(h1, y0 - rad + j));
        arr[k++] = src(x, y);
    }
    
    std::sort(arr.begin(), arr.end());
    res(x0, y0) = arr[N / 2];
}

void Median::ProcessUnchecked(int x0, int y0)
{
    int k = 0;
    
    for (int j = 0; j < diam; j++)
    for (int i = 0; i < diam; i++)
    {
        arr[k++] = src(x0 - rad + i, y0 - rad + j);
    }

    std::sort(arr.begin(), arr.end());
    res(x0, y0) = arr[N / 2];
}

void MedianFilter(const Image<float> &src, int rad, Image<float> &res)
{
    Median(src, rad, res).Perform();
}

int main(int argc, char **argv)
{
    MeasureExecutionTime([](){ }, "Empty action");    
    
    Image<float> img1(size, size), img2(size, size), res(size, size), kernel(7, 7);
    Init(img1, 1.0f);
    Init(img2, 2.0f);
    Init(kernel, 1.0f);
    
    MeasureExecutionTime([&](){ ImageSum(img1, img2, res); }, "Sum: naive");
    MeasureExecutionTime([&](){ ImageSumOptimized(img1, img2, res); }, "Sum: optimized");
    MeasureExecutionTime([&](){ ImageSumUsingAvx(img1, img2, res); }, "Sum: avx");
    MeasureExecutionTime([&](){ Rotate180(img1, res); }, "Rotate: naive");
    MeasureExecutionTime([&](){ Rotate180Optimized(img1, res); }, "Rotate: optimized");
    MeasureExecutionTime([&](){ Rotate180UsingAvx(img1, res); }, "Rotate: avx");
    MeasureExecutionTime([&](){ MedianFilter(img1, 1, res); }, "Median 3x3");
    MeasureExecutionTime([&](){ MedianFilter(img1, 2, res); }, "Median 5x5");
    MeasureExecutionTime([&](){ MedianFilter(img1, 3, res); }, "Median 7x7");
    MeasureExecutionTime([&](){ ImageConvolution(img1, kernel, res); }, "Convolution: naive");
    MeasureExecutionTime([&](){ ImageConvolutionOptimized(img1, kernel, res); }, "Convolution: optimized");
    MeasureExecutionTime([&](){ ImageConvolutionUsingAvx(img1, kernel, res); }, "Convolution: avx");
    MeasureExecutionTime([&](){ ImageConvolutionUsingAvxIsolated(img1, kernel, res); }, "Convolution [*]: avx");
    
    return 0;
}
