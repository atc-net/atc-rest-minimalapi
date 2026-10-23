namespace Demo.Api.Contracts.EndpointDefinitions;

public sealed class UsersEndpointDefinition : IEndpointDefinition
{
    internal const string ApiRouteBase = "/api/users";

    public void DefineEndpoints(WebApplication app)
    {
        var users = app.NewVersionedApi(ApiExplorerGroupNames.Users);

        DefineEndpointsV1(users);
    }

    private void DefineEndpointsV1(IEndpointRouteBuilder app)
    {
        var usersV1 = app
            .MapGroup(ApiRouteBase)
            .HasApiVersion(1.0);

        usersV1
            .MapGet("/", GetAllUsers)
            .WithName(Names.UserDefinitionNames.GetAllUsers)
            .WithDescription("Retrieve all users.")
            .WithSummary("Retrieve all users.");

        usersV1
            .MapGet("/{userId}", GetUserById)
            .WithName(Names.UserDefinitionNames.GetUserById)
            .WithDescription("Get user by specified id.")
            .WithSummary("Get user by specified id.");

        usersV1
            .MapGet("/email", GetUserByEmail)
            .WithName(Names.UserDefinitionNames.GetUserByEmail)
            .WithDescription("Get user by email.")
            .WithSummary("Get user by email.");

        usersV1
            .MapPost("/", CreateUser)
            .WithName(Names.UserDefinitionNames.CreateUser)
            .WithDescription("Create user.")
            .WithSummary("Create user.")
            .AddEndpointFilter<ValidationFilter<CreateUserParameters>>()
            .ProducesValidationProblem();

        usersV1
            .MapPost("/import", ImportUsers)
            .WithName(Names.UserDefinitionNames.ImportUsers)
            .WithDescription("Import users: each user is validated as on create, and an error names the user by its index.")
            .WithSummary("Import users.")
            .AddEndpointFilter<ValidationFilter<ImportUsersParameters>>()
            .ProducesValidationProblem();

        usersV1
            .MapPut("/{userId}", UpdateUserById)
            .WithName(Names.UserDefinitionNames.UpdateUserById)
            .WithDescription("Update user.")
            .WithSummary("Update user.")
            .AddEndpointFilter<ValidationFilter<UpdateUserByIdParameters>>()
            .ProducesValidationProblem();

        usersV1
            .MapDelete("/{userId}", DeleteUserById)
            .WithName(Names.UserDefinitionNames.DeleteUserById)
            .WithDescription("Delete user.")
            .WithSummary("Delete user.");
    }

    internal Task<Ok<IEnumerable<User>>> GetAllUsers(
        [FromServices] IGetUsersHandler handler,
        CancellationToken cancellationToken)
        => handler.ExecuteAsync(cancellationToken);

    internal Task<Results<Ok<User>, NotFound>> GetUserById(
        [FromServices] IGetUserByIdHandler handler,
        [AsParameters] GetUserByIdParameters parameters,
        CancellationToken cancellationToken)
        => handler.ExecuteAsync(parameters, cancellationToken);

    internal Task<Results<Ok<User>, NotFound>> GetUserByEmail(
        [FromServices] IGetUserByEmailHandler handler,
        [AsParameters] GetUserByEmailParameters parameters,
        CancellationToken cancellationToken)
        => handler.ExecuteAsync(parameters, cancellationToken);

    internal Task<Results<CreatedAtRoute, ProblemHttpResult>> CreateUser(
        [FromServices] ICreateUserHandler handler,
        [AsParameters] CreateUserParameters parameters,
        CancellationToken cancellationToken)
        => handler.ExecuteAsync(parameters, cancellationToken);

    internal Task<Results<Ok<int>, ProblemHttpResult>> ImportUsers(
        [FromServices] IImportUsersHandler handler,
        [AsParameters] ImportUsersParameters parameters,
        CancellationToken cancellationToken)
        => handler.ExecuteAsync(parameters, cancellationToken);

    internal Task<Results<Ok<User>, NotFound, ProblemHttpResult>> UpdateUserById(
        [FromServices] IUpdateUserByIdHandler handler,
        [AsParameters] UpdateUserByIdParameters parameters,
        CancellationToken cancellationToken)
        => handler.ExecuteAsync(parameters, cancellationToken);

    internal Task<Results<NoContent, NotFound, ProblemHttpResult>> DeleteUserById(
        [FromServices] IDeleteUserByIdHandler handler,
        [AsParameters] DeleteUserByIdParameters parameters,
        CancellationToken cancellationToken)
        => handler.ExecuteAsync(parameters, cancellationToken);
}