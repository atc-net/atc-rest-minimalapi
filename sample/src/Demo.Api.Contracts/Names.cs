namespace Demo.Api.Contracts;

[SuppressMessage("Design", "CA1034:Do not nest types", Justification = "OK.")]
public static class Names
{
    public static class UserDefinitionNames
    {
        public const string GetAllUsers = nameof(UsersEndpointDefinition.GetAllUsers);
        public const string GetUserById = nameof(UsersEndpointDefinition.GetUserById);
        public const string GetUserByEmail = nameof(UsersEndpointDefinition.GetUserByEmail);
        public const string CreateUser = nameof(UsersEndpointDefinition.CreateUser);
        public const string UpdateUserById = nameof(UsersEndpointDefinition.UpdateUserById);
        public const string DeleteUserById = nameof(UsersEndpointDefinition.DeleteUserById);
    }
}