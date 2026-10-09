namespace Atc.Rest.MinimalApi.Tests.Filters.Endpoints.ValidationBehaviour;

/// <summary>Nested objects, collections, dictionaries, depth and cycles.</summary>
[Collection(ValidationBehaviourGroup.Name)]
public sealed class ObjectGraphBehaviourTests(ValidationBehaviourFixture fixture)
{
    [Fact]
    public async Task RequiredNestedObject_Null()
    {
        // Act
        var response = await fixture.Default.PostJsonAsync("/graph", """{"address":null}""");

        // Assert
        response.ShouldBeValidationProblem(("address", "The Address field is required."));
    }

    [Fact]
    public async Task OptionalNestedObject_Null_IsValid()
    {
        // Act
        var response = await fixture.Default.PostJsonAsync("/graph", """{"address":{"city":"Aarhus"},"optionalAddress":null}""");

        // Assert
        response.ShouldBeOk();
    }

    [Fact]
    public async Task NestedObject_PropertyErrors_AreKeyedByThePath()
    {
        // Act
        var response = await fixture.Default.PostJsonAsync(
            "/graph",
            """{"address":{"city":null},"optionalAddress":{"city":"a city name too long"}}""");

        // Assert
        response.ShouldBeValidationProblem(
            ("address.city", "The City field is required."),
            ("optionalAddress.city", "The field City must be a string or array type with a maximum length of '10'."));
    }

    [Fact]
    public async Task CollectionOfObjects_KeepsTheRowIndex()
    {
        // Act - two invalid rows that are not equal records (null and "")
        var response = await fixture.Default.PostJsonAsync(
            "/graph",
            """{"address":{"city":"Aarhus"},"lines":[{"sku":"a"},{"sku":null},{"sku":""}]}""");

        // Assert
        response.ShouldBeValidationProblem(
            ("lines[1].sku", "The Sku field is required."),
            ("lines[2].sku", "The Sku field is required."));
    }

    [Fact]
    public async Task CollectionWithANullRow_TheNullRowIsSkipped()
    {
        // Act
        var response = await fixture.Default.PostJsonAsync(
            "/graph",
            """{"address":{"city":"Aarhus"},"lines":[{"sku":"a"},null,{"sku":null}]}""");

        // Assert - the index still counts the null row
        response.ShouldBeValidationProblem(("lines[2].sku", "The Sku field is required."));
    }

    [Fact]
    public async Task ArrayOfObjects_KeepsTheRowIndex()
    {
        // Act - GraphLine[] instead of a list
        var response = await fixture.Default.PostJsonAsync("/array", """{"rows":[{"sku":"a"},{"sku":""}]}""");

        // Assert
        response.ShouldBeValidationProblem(("rows[1].sku", "The Sku field is required."));
    }

    [Fact]
    public async Task EqualInvalidRows_AreAllReported()
    {
        // Act - rows 0, 2 and 3 are equal records ({"sku":null})
        var response = await fixture.Default.PostJsonAsync(
            "/graph",
            """{"address":{"city":"Aarhus"},"lines":[{"sku":null},{"sku":"x"},{"sku":null},{"sku":null}]}""");

        // Assert - every row, although records compare by value (ISSUE-17: MiniValidation reported only the first)
        response.ShouldBeValidationProblem(
            ("lines[0].sku", "The Sku field is required."),
            ("lines[2].sku", "The Sku field is required."),
            ("lines[3].sku", "The Sku field is required."));
    }

    [Fact]
    public async Task EqualInvalidRowsOfAClass_AreAllReported()
    {
        // Act - the same content as the Bug17 rows, but the row type is a class (reference equality)
        var response = await fixture.Default.PostJsonAsync(
            "/graph",
            """{"address":{"city":"Aarhus"},"classLines":[{"sku":null},{"sku":"x"},{"sku":null}]}""");

        // Assert
        response.ShouldBeValidationProblem(
            ("classLines[0].sku", "The Sku field is required."),
            ("classLines[2].sku", "The Sku field is required."));
    }

    [Fact]
    public async Task EqualInvalidObjectsInTwoProperties_AreBothReported()
    {
        // Act - address and optionalAddress are equal records ({"city":null})
        var response = await fixture.Default.PostJsonAsync("/graph", """{"address":{"city":null},"optionalAddress":{"city":null}}""");

        // Assert - both, although the records are equal (ISSUE-17)
        response.ShouldBeValidationProblem(
            ("address.city", "The City field is required."),
            ("optionalAddress.city", "The City field is required."));
    }

    [Fact]
    public async Task DictionaryValue_IsReportedByPosition()
    {
        // Act
        var response = await fixture.Default.PostJsonAsync(
            "/graph",
            """{"address":{"city":"Aarhus"},"map":{"first":{"sku":"a"},"second":{"sku":null}}}""");

        // Assert - MiniValidation reports a dictionary entry by its position, not by its key (README: Dictionaries)
        response.ShouldBeValidationProblem(("map[1].Value.Sku", "The Sku field is required."));
    }

    [Fact]
    public async Task RecursiveType_FiveLevelsDeep_IsValidated()
    {
        // Act
        var response = await fixture.Default.PostJsonAsync(
            "/graph",
            """{"address":{"city":"Aarhus"},"node":{"child":{"child":{"child":{"child":{"name":"abcd"}}}}}}""");

        // Assert
        response.ShouldBeValidationProblem(
            ("node.child.child.child.child.name", "The field Name must be a string or array type with a maximum length of '3'."));
    }

    [Fact]
    public async Task RecursiveType_AtTheDepthLimit_IsValidated()
    {
        // Act - 31 child levels below node: the deepest object MiniValidation still visits (MaxDepth 32, counted from
        // the parameters record)
        var response = await fixture.Default.PostJsonAsync("/graph", DeepNodeJson(children: 31));

        // Assert
        response.ShouldBeValidationProblem(
            ("node." + string.Concat(Enumerable.Repeat("child.", 31)) + "name", "The field Name must be a string or array type with a maximum length of '3'."));
    }

    [Theory]
    [InlineData("abcd")] // invalid at the bottom
    [InlineData("abc")] // valid at the bottom: still not validated, so still rejected
    public async Task RecursiveType_BeyondTheDepthLimit_IsRejected(
        string bottomName)
    {
        // Act - one level deeper than validation goes
        var response = await fixture.Default.PostJsonAsync("/graph", DeepNodeJson(children: 32, bottomName));

        // Assert - never a silent pass: the body as a whole is rejected
        response.ShouldBeValidationProblem(("", "The request body is nested too deeply to validate (more than 32 levels)."));
    }

    [Fact]
    public async Task MemberPath_BeyondTheDepthLimit_IsRejectedUnderTheEmptyKey()
    {
        // Act
        var response = await fixture.MemberPath.PostJsonAsync("/graph", DeepNodeJson(children: 32));

        // Assert
        response.ShouldBeValidationProblem(("", "The request body is nested too deeply to validate (more than 32 levels)."));
    }

    private static string DeepNodeJson(
        int children,
        string bottomName = "abcd")
        => "{\"address\":{\"city\":\"Aarhus\"},\"node\":" +
           string.Concat(Enumerable.Repeat("{\"child\":", children)) +
           "{\"name\":\"" + bottomName + "\"}" +
           new string('}', children) +
           "}";

    [Fact]
    public async Task Cycle_DoesNotHang_AndReportsTheErrorOnce()
    {
        // Act - CyclicRequest.BindAsync points Self back at the request
        var response = await fixture.Default.PostJsonAsync("/cyclic", """{"name":"abcd"}""");

        // Assert
        response.ShouldBeValidationProblem(
            ("name", "The field Name must be a string or array type with a maximum length of '3'."));
    }
}