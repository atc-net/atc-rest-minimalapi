namespace Demo.Domain.Users.Handlers;

public sealed class DeleteUserByIdHandler : IDeleteUserByIdHandler
{
    private readonly DemoDbContext dbContext;

    public DeleteUserByIdHandler(DemoDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<Results<NoContent, NotFound, ProblemHttpResult>> ExecuteAsync(
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

        try
        {
            var saveChangesResult = await dbContext.SaveChangesAsync(cancellationToken);

            return saveChangesResult > 0
                ? TypedResults.NoContent()
                : TypedResults.Problem(detail: $"Could not delete user with id '{parameters.UserId}'.", statusCode: StatusCodes.Status400BadRequest);
        }
        catch (DbUpdateException)
        {
            return TypedResults.Problem(detail: $"Could not delete user with id '{parameters.UserId}' due to a database error.", statusCode: StatusCodes.Status400BadRequest);
        }
    }
}