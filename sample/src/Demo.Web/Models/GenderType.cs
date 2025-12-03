namespace Demo.Web.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GenderType
{
    None,
    NonBinary,
    Male,
    Female,
}