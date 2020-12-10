#pragma once

#include <functional>

void MeasureExecutionTime(std::function<void()> func, const char *name, int factor = 1);
