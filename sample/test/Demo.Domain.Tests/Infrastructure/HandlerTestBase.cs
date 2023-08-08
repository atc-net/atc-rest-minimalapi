namespace Demo.Domain.Tests.Infrastructure;

public abstract class HandlerTestBase : IDisposable
{
    protected HandlerTestBase()
    {
        var optionsBuilder = new DbContextOptionsBuilder<DemoDbContext>();
        optionsBuilder = optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        Context = new DemoDbContext(optionsBuilder.Options);
    }

    protected DemoDbContext Context { get; set; }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(
        bool disposing)
    {
        if (disposing)
        {
            Context.Dispose();
        }
    }
}