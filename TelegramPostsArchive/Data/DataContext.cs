using Microsoft.EntityFrameworkCore;
using TelegramPostsArchive.Data.Entities;

namespace TelegramPostsArchive.Data
{
    public class DataContext : DbContext
    {
        public DataContext(DbContextOptions<DataContext> options)
            : base(options)
        {
        }

        public DbSet<TelegramChannel> TelegramChannels => Set<TelegramChannel>();
        public DbSet<TelegramPost> TelegramPosts => Set<TelegramPost>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var channels = modelBuilder.Entity<TelegramChannel>();
            channels.HasKey(channel => channel.ChannelTag);
            channels.Property(channel => channel.ChannelTag).HasMaxLength(32);
            channels.HasData(TelegramChannel.SeedTags.Select(tag => new TelegramChannel { ChannelTag = tag }));

            var posts = modelBuilder.Entity<TelegramPost>();
            posts.HasKey(post => new { post.ChannelTag, post.MessageId });
            posts.HasOne<TelegramChannel>().WithMany().HasForeignKey(post => post.ChannelTag)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

