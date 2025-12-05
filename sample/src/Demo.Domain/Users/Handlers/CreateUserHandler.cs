namespace Demo.Domain.Users.Handlers;

public sealed class CreateUserHandler(DemoDbContext dbContext) : ICreateUserHandler
{
    public async Task<Results<CreatedAtRoute, BadRequest<string>, Conflict<string>>> ExecuteAsync(
        CreateUserParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var existingUserByEmail = await dbContext.Users
            .FirstOrDefaultAsync(
                x => x.Email.Equals(parameters.Request.Email, StringComparison.OrdinalIgnoreCase),
                cancellationToken);

        if (existingUserByEmail is not null)
        {
            return TypedResults.Conflict($"A user already exists with the email '{parameters.Request.Email}'");
        }

        var userId = Guid.NewGuid();
        var userEntity = parameters.Request.Adapt<UserEntity>();
        userEntity.Id = userId;

        dbContext.Users.Add(userEntity);

        try
        {
            var saveChangesResult = await dbContext.SaveChangesAsync(cancellationToken);

            return saveChangesResult > 0
                ? TypedResults.CreatedAtRoute(Api.Contracts.Names.UserDefinitionNames.GetUserById, new { userId })
                : TypedResults.BadRequest("Could not create user.");
        }
        catch (DbUpdateException)
        {
            return TypedResults.BadRequest("Could not create user due to a database error.");
        }
    }
}