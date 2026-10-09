namespace Atc.Rest.MinimalApi.Tests.Models;

/// <summary>
/// Shaped like the generator's <c>RequestBodyList&lt;T&gt;</c>: a list body that binds itself (JSON only here).
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "The Minimal APIs BindAsync contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Mirrors the generator's type.")]
public sealed class BindAsyncList<T> : List<T>
{
    public static async ValueTask<BindAsyncList<T>?> BindAsync(
        HttpContext context,
        System.Reflection.ParameterInfo parameter)
    {
        var items = await context.Request.ReadFromJsonAsync<List<T>>(context.RequestAborted);
        if (items is null)
        {
            return null;
        }

        var list = new BindAsyncList<T>();
        list.AddRange(items);
        return list;
    }
}