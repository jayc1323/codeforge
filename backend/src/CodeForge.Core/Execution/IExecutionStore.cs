namespace CodeForge.Core.Execution;

public interface IExecutionStore
{
    void Add(ExecutionRecord record);
    ExecutionRecord? Get(Guid id);
}
