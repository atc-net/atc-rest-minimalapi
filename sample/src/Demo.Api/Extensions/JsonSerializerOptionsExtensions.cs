namespace Demo.Api.Extensions;

public static class JsonSerializerOptionsExtensions
{
    public static Microsoft.AspNetCore.Http.Json.JsonOptions Configure(
        this JsonSerializerOptions jsonSerializerOptions,
        Microsoft.AspNetCore.Http.Json.JsonOptions options)
    {
        options.SerializerOptions.DefaultIgnoreCondition = jsonSerializerOptions.DefaultIgnoreCondition;
        options.SerializerOptions.PropertyNameCaseInsensitive = jsonSerializerOptions.PropertyNameCaseInsensitive;
        options.SerializerOptions.WriteIndented = jsonSerializerOptions.WriteIndented;
        options.SerializerOptions.PropertyNamingPolicy = jsonSerializerOptions.PropertyNamingPolicy;

        foreach (var jsonConverter in jsonSerializerOptions.Converters)
        {
            options.SerializerOptions.Converters.Add(jsonConverter);
        }

        return options;
    }

    public static JsonOptions Configure(
        this JsonSerializerOptions jsonSerializerOptions,
        JsonOptions options)
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = jsonSerializerOptions.DefaultIgnoreCondition;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = jsonSerializerOptions.PropertyNameCaseInsensitive;
        options.JsonSerializerOptions.WriteIndented = jsonSerializerOptions.WriteIndented;
        options.JsonSerializerOptions.PropertyNamingPolicy = jsonSerializerOptions.PropertyNamingPolicy;

        foreach (var jsonConverter in jsonSerializerOptions.Converters)
        {
            options.JsonSerializerOptions.Converters.Add(jsonConverter);
        }

        return options;
    }
}