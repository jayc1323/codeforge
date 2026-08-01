namespace CodeForge.Core.Execution;

public interface IExecutionProgress
{
    /// <param name="stream">"stdout" or "stderr"</param>
    Task OutputAsync(string stream, string chunk);
}
