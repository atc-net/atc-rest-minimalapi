namespace Demo.Domain.Users.Handlers;

public sealed class GetUsersHandler : IGetUsersHandler
{
    private readonly DemoDbContext dbContext;

    public GetUsersHandler(
        DemoDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<Ok<IEnumerable<User>>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await dbContext.Users
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(users.Adapt<IEnumerable<User>>());
    }
}