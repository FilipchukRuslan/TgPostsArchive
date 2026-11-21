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
            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var channelTag in TelegramChannel.SeedTags)
                {
                    stoppingToken.ThrowIfCancellationRequested();

                    // Collect new posts
                    try
                    {
                        await using var scope = scopeFactory.CreateAsyncScope();
                        var collector = scope.ServiceProvider
                            .GetRequiredService<TelegramCollector>();

                        await collector.GetNewMessageAsync(channelTag, stoppingToken);
                    }
                    catch (Exception ex) when (
                        ex is not OperationCanceledException ||
                        !stoppingToken.IsCancellationRequested)
                    {
                        logger.LogError(ex,
                            "{Channel}: polling failed.", channelTag);
                    }

                    // Continue historical collection
                    try
                    {
                        await using var scope = scopeFactory.CreateAsyncScope();
                        var collector = scope.ServiceProvider
                            .GetRequiredService<TelegramCollector>();

                        await collector.BackfillAsync(
                            channelTag,
                            null,
                            stoppingToken);
                    }
                    catch (Exception ex) when (
                        ex is not OperationCanceledException ||
                        !stoppingToken.IsCancellationRequested)
                    {
                        logger.LogError(ex,
                            "{Channel}: backfill failed.", channelTag);
                    }
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