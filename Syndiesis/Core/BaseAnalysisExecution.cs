﻿using Syndiesis.Core.DisplayAnalysis;
using Syndiesis.Utilities;
using System.Threading;
using System.Threading.Tasks;

namespace Syndiesis.Core;

public abstract class BaseAnalysisExecution(SingleTreeCompilationSource compilationSource)
{
    public AnalysisNodeCreationOptions CreationOptions { get; set; } = new();
    public SingleTreeCompilationSource CompilationSource { get; } = compilationSource;

    protected AnalysisNodeCreatorContainer CreateCreatorContainer()
    {
        return new(CreationOptions);
    }

    public Task<AnalysisResult> ExecuteForCurrentCompilation(CancellationToken token)
    {
        return ExecuteCore(token);
    }

    public Task<AnalysisResult> Execute(
        string? source,
        CancellationToken token)
    {
        if (source is null)
        {
            return ExecuteForCurrentCompilation(token);
        }

        CompilationSource.SetSource(source, token);
        if (token.IsCancellationRequested)
            return Cancelled();

        return ExecuteCore(token);
    }

    protected abstract Task<AnalysisResult> ExecuteCore(CancellationToken token);

    protected static Task<AnalysisResult> Cancelled()
    {
        return Task.FromResult<AnalysisResult>(Singleton<CancelledAnalysisResult>.Instance);
    }
}
