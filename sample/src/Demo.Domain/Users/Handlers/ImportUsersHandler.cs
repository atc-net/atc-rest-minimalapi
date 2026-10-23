namespace Demo.Domain.Users.Handlers;

/// <summary>
/// Imports a list of users. The ValidationFilter has validated each one before this runs (as on create); here only what
/// needs the data: no e-mail address may be taken, by a stored user or by another user of the list.
/// </summary>
public sealed class ImportUsersHandler(DemoDbContext dbContext) : IImportUsersHandler
{
    public async Task<Results<Ok<int>, ProblemHttpResult>> ExecuteAsync(
        ImportUsersParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var duplicate = parameters.Request
            .GroupBy(u => u.Email, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            return TypedResults.Problem(detail: $"The email '{duplicate.Key}' is in the list more than once.", statusCode: StatusCodes.Status409Conflict);
        }

        var emails = new HashSet<string>(parameters.Request.Select(u => u.Email), StringComparer.OrdinalIgnoreCase);
        var storedEmails = await dbContext.Users
            .Select(u => u.Email)
            .ToListAsync(cancellationToken);
        var taken = storedEmails.Find(emails.Contains);
        if (taken is not null)
        {
            return TypedResults.Problem(detail: $"A user already exists with the email '{taken}'.", statusCode: StatusCodes.Status409Conflict);
        }

        foreach (var request in parameters.Request)
        {
            var userEntity = request.Adapt<UserEntity>();
            userEntity.Id = Guid.NewGuid();
            dbContext.Users.Add(userEntity);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(parameters.Request.Count);
    }
}