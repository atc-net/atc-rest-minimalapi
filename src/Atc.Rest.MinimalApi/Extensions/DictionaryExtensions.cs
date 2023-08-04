namespace Atc.Rest.MinimalApi.Extensions;

internal static class DictionaryExtensions
{
    /// <summary>
    /// Merges two dictionaries containing errors and ensures there are no duplicate values for the same key.
    /// </summary>
    /// <param name="errorsA">The first dictionary of errors to merge.</param>
    /// <param name="errorsB">The second dictionary of errors to merge.</param>
    /// <returns>A new dictionary which includes the keys and values from both input dictionaries.
    /// If the same key exists in both dictionaries, the values are merged and duplicates are removed.</returns>
    internal static IDictionary<string, string[]> MergeErrors(
        this IDictionary<string, string[]> errorsA,
        IDictionary<string, string[]> errorsB)
    {
        if (errorsA.Count == 0 &&
            errorsB.Count == 0)
        {
            return new Dictionary<string, string[]>(StringComparer.Ordinal);
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