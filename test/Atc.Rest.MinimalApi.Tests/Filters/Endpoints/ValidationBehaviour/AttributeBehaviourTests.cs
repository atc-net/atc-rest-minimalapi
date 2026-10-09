namespace Atc.Rest.MinimalApi.Tests.Filters.Endpoints.ValidationBehaviour;

/// <summary>Every DataAnnotations attribute with the message a client receives.</summary>
[Collection(ValidationBehaviourGroup.Name)]
public sealed class AttributeBehaviourTests(ValidationBehaviourFixture fixture)
{
    [Theory]
    [InlineData("{}")]
    [InlineData("""{"name":null}""")]
    [InlineData("""{"name":""}""")]
    [InlineData("""{"name":"   "}""")]
    public async Task Required_MissingEmptyOrWhitespace(string json)
    {
        // Act
        var response = await fixture.Default.PostJsonAsync("/required", json);

        // Assert
        response.ShouldBeValidationProblem(("name", "The Name field is required."));
    }

    [Theory]
    [InlineData("""{"stringLengthMax":"abcdef"}""", "stringLengthMax", "The field StringLengthMax must be a string with a maximum length of 5.")]
    [InlineData("""{"stringLengthMinMax":"a"}""", "stringLengthMinMax", "The field StringLengthMinMax must be a string with a minimum length of 2 and a maximum length of 5.")]
    [InlineData("""{"minLengthString":"a"}""", "minLengthString", "The field MinLengthString must be a string or array type with a minimum length of '2'.")]
    [InlineData("""{"maxLengthString":"abcd"}""", "maxLengthString", "The field MaxLengthString must be a string or array type with a maximum length of '3'.")]
    [InlineData("""{"minLengthList":[1]}""", "minLengthList", "The field MinLengthList must be a string or array type with a minimum length of '2'.")]
    [InlineData("""{"maxLengthList":[1,2,3]}""", "maxLengthList", "The field MaxLengthList must be a string or array type with a maximum length of '2'.")]
    [InlineData("""{"rangeInt":11}""", "rangeInt", "The field RangeInt must be between 1 and 10.")]
    [InlineData("""{"rangeLong":11}""", "rangeLong", "The field RangeLong must be between 1 and 10.")]
    [InlineData("""{"rangeDouble":2}""", "rangeDouble", "The field RangeDouble must be between 0.5 and 1.5.")]
    [InlineData("""{"rangeDecimal":2}""", "rangeDecimal", "The field RangeDecimal must be between 0.5 and 1.5.")]
    [InlineData("""{"rangeDate":"2021-06-01"}""", "rangeDate", "The field RangeDate must be between 01/01/2020 00:00:00 and 12/31/2020 00:00:00.")]
    [InlineData("""{"pattern":"ABC"}""", "pattern", "The field Pattern must match the regular expression '^[a-z]+$'.")]
    [InlineData("""{"email":"not-an-email"}""", "email", "The Email field is not a valid e-mail address.")]
    [InlineData("""{"website":"not a url"}""", "website", "The Website field is not a valid fully-qualified http, https, or ftp URL.")]
    [InlineData("""{"phoneNumber":"abc"}""", "phoneNumber", "The PhoneNumber field is not a valid phone number.")]
    [InlineData("""{"password":"a","confirmPassword":"b"}""", "confirmPassword", "'ConfirmPassword' and 'Password' do not match.")]
    [InlineData("""{"allowed":"blue"}""", "allowed", "The Allowed field does not equal any of the values specified in AllowedValuesAttribute.")]
    [InlineData("""{"denied":"blue"}""", "denied", "The Denied field equals one of the values specified in DeniedValuesAttribute.")]
    [InlineData("""{"lengthString":"a"}""", "lengthString", "The field LengthString must be a string or collection type with a minimum length of '2' and maximum length of '3'.")]
    [InlineData("""{"lengthList":[1]}""", "lengthList", "The field LengthList must be a string or collection type with a minimum length of '2' and maximum length of '3'.")]
    [InlineData("""{"base64":"***"}""", "base64", "The Base64 field is not a valid Base64 encoding.")]
    [InlineData("""{"even":3}""", "even", "The field Even must be an even number.")]
    [InlineData("""{"customMessage":"abcd"}""", "customMessage", "CustomMessage is too long (max 3).")]
    [InlineData("""{"resourceMessage":"abcd"}""", "resourceMessage", "ResourceMessage may have at most 3 characters.")]
    [InlineData("""{"nickName":"abcd"}""", "nickName", "The field Nick name must be a string or array type with a maximum length of '3'.")]
    public async Task Attribute_ReportsItsMessageUnderTheWireName(
        string json,
        string expectedKey,
        string expectedMessage)
    {
        // Act
        var response = await fixture.Default.PostJsonAsync("/attributes", json);

        // Assert
        response.ShouldBeValidationProblem((expectedKey, expectedMessage));
    }

    [Fact]
    public async Task Attributes_AllValid_ReachesTheHandler()
    {
        // Arrange - every value at the edge of its rule
        const string json = """
            {
              "stringLengthMax":"abcde","stringLengthMinMax":"ab","minLengthString":"ab","maxLengthString":"abc",
              "minLengthList":[1,2],"maxLengthList":[1,2],"rangeInt":10,"rangeLong":1,"rangeDouble":1.5,
              "rangeDecimal":0.5,"rangeDate":"2020-06-01","pattern":"abc","email":"a@b.dk","website":"https://atc.dk",
              "phoneNumber":"+45 12 34 56 78","password":"x","confirmPassword":"x","allowed":"red","denied":"red",
              "lengthString":"abc","lengthList":[1,2],"base64":"YWJj","even":4,"customMessage":"abc",
              "resourceMessage":"abc","nickName":"abc"
            }
            """;

        // Act
        var response = await fixture.Default.PostJsonAsync("/attributes", json);

        // Assert
        response.ShouldBeOk();
    }

    [Fact]
    public async Task Attributes_SeveralFail_EachIsReported()
    {
        // Act
        var response = await fixture.Default.PostJsonAsync("/attributes", """{"rangeInt":0,"pattern":"A","even":1}""");

        // Assert
        response.ShouldBeValidationProblem(
            ("rangeInt", "The field RangeInt must be between 1 and 10."),
            ("pattern", "The field Pattern must match the regular expression '^[a-z]+$'."),
            ("even", "The field Even must be an even number."));
    }

    [Fact]
    public async Task InitOnlyClass_IsValidatedLikeARecord()
    {
        // Act
        var response = await fixture.Default.PostJsonAsync("/init-only", """{"name":"abcd"}""");

        // Assert
        response.ShouldBeValidationProblem(
            ("code", "The Code field is required."),
            ("name", "The field Name must be a string or array type with a maximum length of '3'."));
    }
}