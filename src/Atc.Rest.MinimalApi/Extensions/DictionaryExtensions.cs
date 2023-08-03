namespace Atc.Rest.MinimalApi.Extensions;

internal static class DictionaryExtensions
{
    internal static IDictionary<string, string[]> MergeErrors(
        this IDictionary<string, string[]> errorsA,
        IDictionary<string, string[]> errorsB)
    {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (!errorsA.Any() &&
            !errorsB.Any())
        {
            return result;
        }

        return errorsA
            .Concat(errorsB)
            .GroupBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x
                    .SelectMany(y => y.Value)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
    }
}