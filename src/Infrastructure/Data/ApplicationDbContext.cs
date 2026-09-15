using Microsoft.EntityFrameworkCore;
using transactionalsystem.Domain.Entities;
using transactionalsystem.Infrastructure.Outbox;

namespace transactionalsystem.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<TransactionalSystemAggregate> TransactionalSystems { get; set; } = null!;
        public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<TransactionalSystemAggregate>(entity =>
            {
                entity.HasKey(e => e.Id);
                // Additional configurations can be added here
            });
            
            modelBuilder.Entity<OutboxMessage>(entity =>
            {
                entity.HasKey(e => e.Id);
            });
        }
    }
}
