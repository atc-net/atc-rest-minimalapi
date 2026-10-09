namespace Atc.Rest.MinimalApi.Tests.Validation;

/// <summary>
/// Edge cases of the DataAnnotations validator that the validation behaviour suite does not reach over HTTP.
/// </summary>
public sealed class DataAnnotationsGraphValidatorTests
{
    [Fact]
    public void ValidatableObject_SuccessAndNullResults_AreIgnored()
    {
        // Act
        var isValid = DataAnnotationsGraphValidator.TryValidate(new QuietValidatable(), out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidatableObject_ResultWithoutMessage_IsReportedWithAnEmptyMessage()
    {
        // Act
        var isValid = DataAnnotationsGraphValidator.TryValidate(new MessagelessValidatable(), out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().ContainKey("Name").WhoseValue.Should().Equal(string.Empty);
    }

    [Fact]
    public void IndexerAndWriteOnlyProperties_AreNotRead()
    {
        // Act - reading either would throw
        var isValid = DataAnnotationsGraphValidator.TryValidate(new UnreadableMembers { Name = "abcd" }, out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Keys.Should().Equal("Name");
    }

    [Fact]
    public void NonGenericCollection_EachItemIsValidatedByItsOwnType()
    {
        // Arrange - the element type of an ArrayList is unknown: each item is checked (as MiniValidation did)
        var target = new NonGenericCollectionHolder { Items = ["text", new UnreadableMembers { Name = "abcd" }] };

        // Act
        DataAnnotationsGraphValidator.TryValidate(target, out var errors);

        // Assert - MiniValidation keyed this "Items.[1].Name"; the index follows the member like any collection
        errors.Keys.Should().Equal("Items[1].Name");
    }

    [Fact]
    public void ObjectMember_IsValidatedByItsRuntimeType()
    {
        // Act
        DataAnnotationsGraphValidator.TryValidate(new ObjectHolder { Value = new SetRequestLine(Sku: null) }, out var errors);

        // Assert
        errors.Keys.Should().Equal("Value.Sku");
    }

    [Fact]
    public void InterfaceMember_IsValidatedByItsRuntimeType()
    {
        // Act
        DataAnnotationsGraphValidator.TryValidate(new InterfaceHolder { Item = new RequiredCode() }, out var errors);

        // Assert
        errors.Keys.Should().Equal("Item.Code");
    }

    [Fact]
    public void CollectionsOfInterfacesOrObjects_EachItemIsValidatedByItsOwnType()
    {
        // Arrange
        var target = new UnknownElementCollections
        {
            Likes = [new RequiredCode()],
            Objects = [new SetRequestLine(Sku: null)],
        };

        // Act
        DataAnnotationsGraphValidator.TryValidate(target, out var errors);

        // Assert
        errors.Keys.Should().BeEquivalentTo("Likes[0].Code", "Objects[0].Sku");
    }

    [Fact]
    public void CollectionMemberWithItsOwnProperties_OnlyTheItemsAreValidated()
    {
        // Act - as MiniValidation: a collection member is walked by its items
        DataAnnotationsGraphValidator.TryValidate(new LabelledLinesHolder { Lines = [new SetRequestLine(Sku: null)] }, out var errors);

        // Assert - the collection's own [Required] Label is not checked
        errors.Keys.Should().Equal("Lines[0].Sku");
    }

    [Fact]
    public void RootCollectionWithItsOwnProperties_PropertiesAndItemsAreValidated()
    {
        // Act - as MiniValidation: the validated object itself is checked by its members and its items
        DataAnnotationsGraphValidator.TryValidate(new LabelledLines { new SetRequestLine(Sku: null) }, out var errors);

        // Assert
        errors.Keys.Should().BeEquivalentTo("Label", "[0].Sku");
    }

    [Fact]
    public void MetadataTypeBuddyClass_IsNotHonoured()
    {
        // Act - as MiniValidation 0.10 (the [Required] is on the buddy class only)
        var isValid = DataAnnotationsGraphValidator.TryValidate(new BuddyTarget(), out _);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void HttpContextMember_IsNotRead()
    {
        // Arrange - reading Request.Form of a request that is not a form throws
        var target = new ContextHolder
        {
            Context = new DefaultHttpContext(),
            Name = "abcd",
        };

        // Act
        var act = () => DataAnnotationsGraphValidator.TryValidate(target, out _);

        // Assert
        act.Should().NotThrow();
        DataAnnotationsGraphValidator.TryValidate(target, out var errors);
        errors.Keys.Should().Equal("Name");
    }

    [Fact]
    public void RootCollection_ItsItemsAreValidatedByIndex()
    {
        // Act
        var isValid = DataAnnotationsGraphValidator.TryValidate(
            new List<SetRequestLine> { new("a"), new(Sku: null) },
            out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Keys.Should().Equal("[1].Sku");
    }

    [Fact]
    public void SkipRecursion_RecognisedByName_StopsTheWalk()
    {
        // Arrange - MiniValidation's [SkipRecursion] (here a look-alike in the MiniValidation namespace)
        var target = new SkipRecursionHolder(Line: new SetRequestLine(Sku: null), Skipped: new SetRequestLine(Sku: null));

        // Act
        DataAnnotationsGraphValidator.TryValidate(target, out var errors);

        // Assert
        errors.Keys.Should().Equal("Line.Sku");
    }

    [Fact]
    public void NullTarget_Throws()
    {
        // Act
        var act = () => DataAnnotationsGraphValidator.TryValidate(null!, out _);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private sealed class QuietValidatable : IValidatableObject
    {
        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            yield return ValidationResult.Success!;
            yield return null!;
        }
    }

    private sealed class MessagelessValidatable : IValidatableObject
    {
        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            yield return new ValidationResult(errorMessage: null, [nameof(Name)]);
        }

        public static string Name => nameof(Name);
    }

    private sealed class UnreadableMembers
    {
        [MaxLength(3)]
        public string? Name { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S2376:Write-only properties should not be used", Justification = "The case under test.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1044:Properties should not be write only", Justification = "The case under test.")]
        [Required]
        public string WriteOnly
        {
            set => throw new InvalidOperationException(value);
        }

        [Required]
        public string this[int index]
            => throw new InvalidOperationException(index.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private sealed class NonGenericCollectionHolder
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2227:Collection properties should be read only", Justification = "The case under test.")]
        public System.Collections.ArrayList? Items { get; set; }
    }

    private sealed class ObjectHolder
    {
        public object? Value { get; init; }
    }

    private interface ICode
    {
        string? Code { get; }
    }

    private sealed class RequiredCode : ICode
    {
        [Required]
        public string? Code { get; init; }
    }

    private sealed class InterfaceHolder
    {
        public ICode? Item { get; init; }
    }

    private sealed class UnknownElementCollections
    {
        public IList<ICode>? Likes { get; init; }

        public IList<object>? Objects { get; init; }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix", Justification = "The case under test.")]
    private sealed class LabelledLines : List<SetRequestLine>
    {
        [Required]
        public string? Label { get; init; }
    }

    private sealed class LabelledLinesHolder
    {
        public LabelledLines? Lines { get; init; }
    }

    [MetadataType(typeof(BuddyMetadata))]
    private sealed class BuddyTarget
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "Read by the validator through reflection.")]
        public string? Name { get; init; }
    }

    private sealed class BuddyMetadata
    {
        [Required]
        public object? Name { get; init; }
    }

    private sealed class ContextHolder
    {
        public HttpContext? Context { get; init; }

        [MaxLength(3)]
        public string? Name { get; init; }
    }

    private sealed record SkipRecursionHolder(
        SetRequestLine Line,
        [property: MiniValidation.SkipRecursion] SetRequestLine Skipped);
}