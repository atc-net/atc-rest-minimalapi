namespace Demo.Domain.Users.Handlers;

public sealed class UpdateUserByIdHandler : IUpdateUserByIdHandler
{
    private readonly DemoDbContext dbContext;

    public UpdateUserByIdHandler(
        DemoDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<Results<Ok<User>, BadRequest<string>, NotFound, Conflict<string>>> ExecuteAsync(
        UpdateUserByIdParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FindAsync(
            parameters.UserId,
            cancellationToken);

        if (user is null)
        {
            return TypedResults.NotFound();
        }

        var existingUserByEmail = await dbContext.Users
            .FirstOrDefaultAsync(
                x => x.Email.Equals(parameters.Request.Email, StringComparison.OrdinalIgnoreCase),
                cancellationToken);

        if (existingUserByEmail is not null)
        {
            return TypedResults.Conflict($"A user already exists with the email '{parameters.Request.Email}'");
        }

        if (!IsModified(parameters.Request, user))
        {
            return TypedResults.Ok(user.Adapt<User>());
        }

        user.FirstName = parameters.Request.FirstName;
        user.LastName = parameters.Request.LastName;
        user.Gender = parameters.Request.Gender;
        user.Email = parameters.Request.Email;

        var saveChangesResult = await dbContext.SaveChangesAsync(cancellationToken);

        return saveChangesResult > 0
            ? TypedResults.Ok(user.Adapt<User>())
            : TypedResults.BadRequest("Could not update user.");
    }

    private static bool IsModified(
        UpdateUserRequest request,
        UserEntity user)
        => !request.FirstName.Equals(user.FirstName, StringComparison.Ordinal) ||
           !request.LastName.Equals(user.LastName, StringComparison.Ordinal) ||
           request.Gender != user.Gender ||
           !request.Email.Equals(user.Email, StringComparison.Ordinal);
}