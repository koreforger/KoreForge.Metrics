using System;
using System.Collections.Generic;

namespace KF.Metrics;

public sealed class OperationCompletedContext
{
    public string Name { get; init; } = string.Empty;

    public TimeSpan Duration { get; init; }

    public bool IsFailure { get; init; }

    public int ConcurrencyAtEnd { get; init; }

    public IReadOnlyDictionary<string, string>? Tags { get; init; }

    public DateTimeOffset EndTime { get; init; }

    public double? ProcessCpuPercent { get; init; }
}
