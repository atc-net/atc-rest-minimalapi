namespace Demo.Domain.Storage;

public class DemoDbContext : DbContext
{
    public DemoDbContext(
        DbContextOptions<DemoDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<UserEntity> Users { get; set; } = null!;

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.OwnsOne(e => e.HomeAddress, address =>
            {
                address.OwnsOne(a => a.Country);
            });
            entity.OwnsOne(e => e.WorkAddress, address =>
            {
                address.OwnsOne(a => a.Country);
            });
        });

        base.OnModelCreating(modelBuilder);
    }
}