using CodeForge.Core.Execution;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeForge.Infrastructure.Execution;

public sealed class ExecutionWorker(
    IExecutionQueue queue,
    IExecutionRunner runner,
    IExecutionEventPublisher publisher,
    ILogger<ExecutionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            ExecutionRecord record;
            try
            {
                record = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            record.Status = ExecutionStatus.Running;
            await SafePublishAsync(() => publisher.PublishStatusAsync(record.Id, ExecutionStatus.Running), record.Id);

            try
            {
                var progress = new ProgressForwarder(record.Id, publisher, logger);
                var outcome = await runner.RunAsync(record.Request, stoppingToken, progress);
                record.Status = outcome.Status;
                record.Stdout = outcome.Stdout;
                record.Stderr = outcome.Stderr;
                record.ExitCode = outcome.ExitCode;
                record.Duration = outcome.Duration;

                await SafePublishAsync(() => publisher.PublishCompletedAsync(
                    record.Id, outcome.Status, outcome.Stdout, outcome.Stderr,
                    outcome.ExitCode, outcome.Duration.TotalMilliseconds), record.Id);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Execution {ExecutionId} failed unexpectedly", record.Id);
                record.Status = ExecutionStatus.Failed;
                record.Stderr = ex.Message;
            }
            finally
            {
                record.CompletedAt = DateTimeOffset.UtcNow;
            }
        }
    }

    private async Task SafePublishAsync(Func<Task> publish, Guid executionId)
    {
        try
        {
            await publish();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish event for execution {ExecutionId}", executionId);
        }
    }

    private sealed class ProgressForwarder(
        Guid executionId, IExecutionEventPublisher publisher, ILogger logger) : IExecutionProgress
    {
        public async Task OutputAsync(string stream, string chunk)
        {
            try
            {
                await publisher.PublishOutputAsync(executionId, stream, chunk);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to publish output for execution {ExecutionId}", executionId);
            }
        }
    }
}
