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

        public DbSet<TelegramChannel> Channels => Set<TelegramChannel>();
        public DbSet<TelegramPost> Posts => Set<TelegramPost>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

        }
    }
}

