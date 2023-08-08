namespace Demo.Domain.Users.Handlers;

public sealed class DeleteUserByIdHandler : IDeleteUserByIdHandler
{
    private readonly DemoDbContext dbContext;

    public DeleteUserByIdHandler(
        DemoDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<Results<NoContent, NotFound>> ExecuteAsync(
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
        if (saveChangesResult > 0)
        {
            return TypedResults.NoContent();
        }

        throw new DbUpdateException($"Failed to delete user with id '{parameters.UserId}'");
    }
}