namespace Demo.Domain.Users.Handlers;

public sealed class DeleteUserByIdHandler : IDeleteUserByIdHandler
{
    private readonly DemoDbContext dbContext;

    public DeleteUserByIdHandler(
        DemoDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<Results<NoContent, BadRequest<string>, NotFound>> ExecuteAsync(
        DeleteUserByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FindAsync(
            parameters.UserId,
            cancellationToken);

        if (user is null)
        {
            return TypedResults.NotFound();
        }

        dbContext.Users.Remove(user);

        var saveChangesResult = await dbContext.SaveChangesAsync(cancellationToken);

        return saveChangesResult > 0
            ? TypedResults.NoContent()
            : TypedResults.BadRequest($"Could not delete user with id '{parameters.UserId}'.");
    }
}