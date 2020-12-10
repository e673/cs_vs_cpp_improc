#include "perf.hpp"
#include <chrono>
#include <algorithm>
#include <vector>
#include <cstdio>
#include <numeric>

using namespace std::chrono;

constexpr double ExecuteTime = 1.0;
constexpr double ExecuteProbeTime = 0.1;
constexpr int MaxNameLength = 30;
constexpr int MaxIterationCount = 1000000000;
constexpr int MinBucketCount = 1000;
constexpr int MaxBucketCount = 5000;

double Probe(std::function<void()> func)
{
    int count = 0, limit = 1;
    double elapsed = 0.0;

    while (true)
    {
        auto t1 = high_resolution_clock::now();
        
        for (; count < limit; ++count)
        {
            func();
        }

        auto t2 = high_resolution_clock::now();        
        elapsed += duration_cast<duration<double>>(t2 - t1).count();
        
        if (elapsed >= ExecuteProbeTime)
            return elapsed / count;

        limit *= 2;
        
        // Overflow check
        if (limit < count)
            return elapsed / count;
    }
}

struct FormattedDouble
{
    double v;
    char c;
};

FormattedDouble FormatFixedDouble(double v)
{
    if (v >= 1e12 || v <= 1e-12)
        return { v, ' ' };
    else if (v >= 1e9)
        return { v * 1e-9, 'G' };
    else if (v >= 1e6)
        return { v * 1e-6, 'M' };
    else if (v >= 1e3)
        return { v * 1e-3, 'k' };
    else if (v >= 1e0)
        return { v, ' ' };
    else if (v >= 1e-3)
        return { v * 1e3, 'm' };
    else if (v >= 1e-6)
        return { v * 1e6, 'u' };
    else if (v >= 1e-9)
        return { v * 1e9, 'n' };
    else
        return { v * 1e12, 'p' };
}

void PrintStatistics(double v)
{
    auto fd1 = FormatFixedDouble(v);
    auto fd2 = FormatFixedDouble(1.0 / v);

    char buf1[6], buf2[6];
    snprintf(buf1, 6, "%3lf", fd1.v);
    snprintf(buf2, 6, "%3lf", fd2.v);   
    
    printf("%s %cs/op, %s %cop/s", buf1, fd1.c, buf2, fd2.c);
}

void PrintStatistics(const char *name, std::vector<double>& stat)
{
    std::sort(stat.begin(), stat.end());    
    double average = std::accumulate(stat.begin(), stat.end(), 0.0) / stat.size();
    double pc10 = stat[stat.size() / 10];

    printf("%-30s: ", name);
    PrintStatistics(pc10);
    printf(" | ");
    PrintStatistics(average);
    printf("\n");
}

void MeasureExecutionTime(std::function<void()> func, const char *name, int factor)
{
    // Warmup
    func();
    
    double approx = Probe(func);
    int iterCount = (int) std::min(ExecuteTime / approx, (double)MaxIterationCount);    
    
    if (iterCount <= MaxBucketCount)
    {
        std::vector<double> stat(iterCount);
        
        for (int i = 0; i < iterCount; i++)
        {
            auto t1 = high_resolution_clock::now();
            func();
            auto t2 = high_resolution_clock::now();        
            auto t = duration_cast<duration<double>>(t2 - t1).count();
            stat[i] = t / factor;
        }

        PrintStatistics(name, stat);
    }
    else
    {
        int bucketSize = iterCount / MinBucketCount;
        std::vector<double> stat(MinBucketCount);

        for (int i = 0; i < MinBucketCount; i++)
        {
            auto t1 = high_resolution_clock::now();

            for (int j = 0; j < bucketSize; j++)
            {
                func();
            }

            auto t2 = high_resolution_clock::now();        
            auto t = duration_cast<duration<double>>(t2 - t1).count();
            stat[i] = t / (factor * bucketSize);
        }

        PrintStatistics(name, stat);
    }
}
