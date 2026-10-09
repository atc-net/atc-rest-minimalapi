// A look-alike of MiniValidation's attribute (same full name): the validator recognises [SkipRecursion] by name, so the
// attribute of the MiniValidation package keeps working for consumers who reference it.
namespace MiniValidation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class SkipRecursionAttribute : Attribute
{
}