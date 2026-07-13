using System.Threading.Channels;

namespace Glovelly.Api.Services;

public interface ISetListChartMatchJobQueue
{
    ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default);

    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken = default);
}

internal sealed class SetListChartMatchJobQueue : ISetListChartMatchJobQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
    });

    public ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(jobId, cancellationToken);
    }

    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}
