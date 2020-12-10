using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace PerformanceTests
{
    sealed unsafe class NativeImage<T> : IDisposable
        where T : unmanaged
    {
        const int MaxDimensions = 16384;
        
        public NativeImage(int width, int height)
        {
            if (width <= 0 || width > MaxDimensions)
                throw new ArgumentException($"Width must be positive value not greater than {MaxDimensions}", nameof(width));

            if (height <= 0 || height > MaxDimensions)
                throw new ArgumentException($"Height must be positive value not greater than {MaxDimensions}", nameof(width));

            Width = width;
            Height = height;
            Stride = (width * sizeof(T) + 31) / 32 * 32;

            mem = Marshal.AllocHGlobal(Height * Stride + 31);
            basePtr = new IntPtr((mem.ToInt64() + 31) / 32 * 32);
        }

        IntPtr mem;
        IntPtr basePtr;
        public int Width { get; }
        public int Height { get; }
        public nint Stride { get; }

        // Different methods to access bitmap

#if DEBUG

        public T this[int x, int y]
        {
            [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
            get => x < 0 || x >= Width ? throw new ArgumentOutOfRangeException(nameof(x))
                : y < 0 || y >= Height ? throw new ArgumentOutOfRangeException(nameof(y))
                : *((T*) (basePtr + y * Stride) + x);
            [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
            set => *((T*) (basePtr + y * Stride) + x) =
                x < 0 || x >= Width ? throw new ArgumentOutOfRangeException(nameof(x))
                : y < 0 || y >= Height ? throw new ArgumentOutOfRangeException(nameof(y))
                : value;
        }

#else

        public T this[int x, int y]
        {
            [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
            get => *((T*) (basePtr + y * Stride) + x);
            [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
            set => *((T*) (basePtr + y * Stride) + x) = value;
        }

#endif
        
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
        public ref T Pixel(int x, int y) => ref *((T*) (basePtr + y * Stride) + x);

        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
        public T* PixelAddr(int x, int y) => (T*) (basePtr + y * Stride) + x;
        
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
        public T GetSafe(int x, int y) => x >= 0 && x < Width
            ? y >= 0 && y < Height ? ((T*) ((byte*) basePtr + y * Stride))[x]
            : throw new ArgumentOutOfRangeException(nameof(y))
            : throw new ArgumentOutOfRangeException(nameof(x));

        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
        public T GetUnsafe(int x, int y) => *((T*) (basePtr + y * Stride) + x);

        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
        public void SetUnsafe(int x, int y, T value) => *((T*) (basePtr + y * Stride) + x) = value;

        bool ReleaseMemory()
        {
            if (Interlocked.Exchange(ref basePtr, IntPtr.Zero) == IntPtr.Zero)
                return false;
                
            Marshal.FreeHGlobal(mem);
            mem = IntPtr.Zero;
            return true;
        }

        public void Dispose()
        {
            if (ReleaseMemory())
                GC.SuppressFinalize(this);
        }

        ~NativeImage()
        {
            ReleaseMemory();
        }
    }
}