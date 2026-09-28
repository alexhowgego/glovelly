using System.Threading.Channels;

namespace Glovelly.Api.Services;

public interface ISetListInterpretationJobQueue
{
    ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default);
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken = default);
}

internal sealed class SetListInterpretationJobQueue : ISetListInterpretationJobQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();
    public ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default) => _channel.Writer.WriteAsync(jobId, cancellationToken);
    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken = default) => _channel.Reader.ReadAsync(cancellationToken);
}
