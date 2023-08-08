namespace Demo.Domain.Users.Handlers;

public sealed class GetUserByIdHandler : IGetUserByIdHandler
{
    private readonly DemoDbContext dbContext;

    public GetUserByIdHandler(
        DemoDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<Results<Ok<User>, NotFound>> ExecuteAsync(
        GetUserByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FindAsync(
            parameters.UserId,
            cancellationToken);

        return user is not null
            ? TypedResults.Ok(user.Adapt<User>())
            : TypedResults.NotFound();
    }
}