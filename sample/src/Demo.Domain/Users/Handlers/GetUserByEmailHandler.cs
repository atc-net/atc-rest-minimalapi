namespace Demo.Domain.Users.Handlers;

public sealed class GetUserByEmailHandler : IGetUserByEmailHandler
{
    private readonly DemoDbContext dbContext;

    public GetUserByEmailHandler(
        DemoDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<Results<Ok<User>, NotFound>> ExecuteAsync(
        GetUserByEmailParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(
                x => x.Email.Equals(parameters.Email, StringComparison.OrdinalIgnoreCase),
                cancellationToken);

        return user is not null
            ? TypedResults.Ok(user.Adapt<User>())
            : TypedResults.NotFound();
    }
}