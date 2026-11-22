using Microsoft.Extensions.Options;
using TelegramPostsArchive.Data.Entities;

namespace TelegramPostsArchive.Services.BackgroundWorkers
{
    public sealed class BackgroundCollectionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<PollingOptions> options,
    ILogger<BackgroundCollectionWorker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            await Task.WhenAll(TelegramChannel.SeedTags.Select(
                channelTag => CollectHistoricalPostsAsync(channelTag, stoppingToken)));

            await Task.WhenAll(TelegramChannel.SeedTags.Select(
                channelTag => CollectNewPostsAsync(channelTag, stoppingToken)));
        }

        private async Task CollectHistoricalPostsAsync(string channelTag, CancellationToken stoppingToken)
        {
            while (true)
            {
                stoppingToken.ThrowIfCancellationRequested();
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var collector = scope.ServiceProvider.GetRequiredService<TelegramCollector>();
                    await collector.CollectHistoricalPostsAsync(channelTag, stoppingToken);
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
                {
                    logger.LogError(ex, "{Channel}: historical collection failed.", channelTag);
                }

                await Task.Delay(options.Value.Interval, stoppingToken);
            }
        }

        private async Task CollectNewPostsAsync(string channelTag, CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var collector = scope.ServiceProvider.GetRequiredService<TelegramCollector>();
                    await collector.CollectNewPostsAsync(channelTag, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
                {
                    logger.LogError(ex, "{Channel}: new post collection failed.", channelTag);
                }

                await Task.Delay(options.Value.Interval, stoppingToken);
            }
        }
    }

    public sealed class PollingOptions
    {
        public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);
    }

}