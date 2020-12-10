#pragma once

#include <cstdlib>
#include <stdexcept>
#include <utility>

template <typename T>
class Image
{
public:
    Image(int w, int h);
    Image(Image&& other);
    int Width() const;
    int Height() const;
    T& operator() (int x, int y);
    const T& operator() (int x, int y) const;    
    ~Image();
    
    static constexpr int MaxDimensions = 16384;
    
private:
    void *ptr;
    int width, height, stride;
};

template <typename T>
Image<T>::Image(int w, int h)
{
    if (w < 0 || w >= MaxDimensions || h < 0 || h >= MaxDimensions)
        throw std::out_of_range("Image dimensions are outside allowed range");
    
    width = w;
    height = h;
    stride = (width * sizeof(T) + 31) / 32 * 32;
    ptr = aligned_alloc(32, stride * height);
}

template <typename T>
Image<T>::Image(Image<T>&& other)
{
    std::swap(ptr, other.ptr);
    std::swap(width, other.width);
    std::swap(height, other.height);
    std::swap(stride, other.stride);    
}

template <typename T>
int Image<T>::Width() const { return width; }

template <typename T>
int Image<T>::Height() const { return height; }

template <typename T>
T& Image<T>::operator() (int x, int y) { return *(T*)((char*)ptr + y * stride + x * sizeof(T)); }

template <typename T>
const T& Image<T>::operator() (int x, int y) const { return *(T*)((char*)ptr + y * stride + x * sizeof(T)); }

template <typename T>
Image<T>::~Image()
{
    free(ptr);
}
