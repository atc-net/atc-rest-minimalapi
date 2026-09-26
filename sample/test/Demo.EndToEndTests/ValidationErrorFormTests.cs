namespace Demo.EndToEndTests;

/// <summary>
/// End-to-end proof that the ValidationFilter's error keys (the names the client sent) map straight onto form fields
/// in the React and Blazor samples. Explicit (opt-in): run with
/// <c>dotnet test --project sample/test/Demo.EndToEndTests -- --explicit only</c>.
/// </summary>
[Collection(DemoAppCollectionDefinition.Name)]
public sealed class ValidationErrorFormTests(DemoAppFixture fixture)
{
    // Collects, per MudBlazor input control in the open dialog, whether it shows an error.
    private const string BlazorErrorIndicesScript =
        """
        () => [...document.querySelectorAll('.mud-dialog .mud-input-control')]
            .map((control, index) => ({ index, error: control.classList.contains('mud-input-error') || !!control.querySelector('.mud-input-error') }))
            .filter(x => x.error)
            .map(x => x.index)
        """;

    // Collects the names (= wire paths) of the MUI fields in the open dialog that show an error.
    private const string ReactInvalidFieldNamesScript =
        """
        () => [...document.querySelectorAll('.MuiDialog-root .MuiFormControl-root')]
            .filter(control => control.querySelector('.Mui-error'))
            .map(control => control.querySelector('input[name]')?.name)
        """;

    [Fact(Explicit = true)]
    public async Task React_CreateUser_InvalidSubmit_ShowsEachServerErrorUnderItsField()
    {
        // Arrange - each input's name is its wire path, so errors[name] belongs to that input
        var cancellationToken = TestContext.Current.CancellationToken;
        var page = await OpenReactUsersPageAsync(cancellationToken);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create User" }).ClickAsync();

        await page.FillAsync("input[name='firstName']", "A"); // too short
        await page.FillAsync("input[name='email']", "not-an-email");
        await page.FillAsync("input[name='workAddress.cityName']", "X"); // too short
        await page.FillAsync("input[name='workAddress.country.alpha2Code']", "DNK"); // must be 2 letters

        // Act
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create", Exact = true }).ClickAsync();
        await page.Locator(".MuiDialog-root .Mui-error").First.WaitForAsync();

        // Assert
        var invalidFields = await page.EvaluateAsync<string[]>(ReactInvalidFieldNamesScript);
        invalidFields.Should().BeEquivalentTo(
            "gender", "firstName", "lastName", "email", "workAddress.cityName", "workAddress.country.alpha2Code");
    }

    [Fact(Explicit = true)]
    public async Task React_EditUser_InvalidSubmit_ShowsEachServerErrorUnderItsField()
    {
        // Arrange - UpdateUserRequest sends the work address as "address", so the keys are "address.*"
        var cancellationToken = TestContext.Current.CancellationToken;
        var page = await OpenReactUsersPageAsync(cancellationToken);

        // The seed data picks a random gender; "None" would add a Gender error, so edit a user with another gender.
        await page
            .Locator("tbody tr")
            .Filter(new LocatorFilterOptions { HasNot = page.Locator("td[data-label='Gender']:text-is('None')") })
            .First
            .GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Edit" })
            .ClickAsync();
        await page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Edit User" }).WaitForAsync();

        await page.FillAsync("input[name='firstName']", "A");
        await page.FillAsync("input[name='email']", "not-an-email");
        await page.FillAsync("input[name='address.cityName']", "X");
        await page.FillAsync("input[name='address.country.alpha2Code']", "DNK");

        // Act
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Update", Exact = true }).ClickAsync();
        await page.Locator(".MuiDialog-root .Mui-error").First.WaitForAsync();

        // Assert
        var invalidFields = await page.EvaluateAsync<string[]>(ReactInvalidFieldNamesScript);
        invalidFields.Should().BeEquivalentTo("firstName", "email", "address.cityName", "address.country.alpha2Code");

        var firstNameHelperText = await page.Locator("[id='field-firstName-helper-text']").InnerTextAsync();
        firstNameHelperText.Should().StartWith("'First Name' must be between 2 and 10 characters.");
    }

    [Fact(Explicit = true)]
    public async Task React_CreateUpdateDelete_UserRoundTrip()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var page = await OpenReactUsersPageAsync(cancellationToken);
        var email = $"ada.{Guid.NewGuid():N}@example.com";

        // Create
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create User" }).ClickAsync();
        await page.Locator("[id='field-gender']").ClickAsync();
        await page.GetByRole(AriaRole.Option, new PageGetByRoleOptions { Name = "Female", Exact = true }).ClickAsync();
        await page.FillAsync("input[name='firstName']", "Ada");
        await page.FillAsync("input[name='lastName']", "Tester");
        await page.FillAsync("input[name='email']", email);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create", Exact = true }).ClickAsync();

        await page.GetByText("User created successfully!").WaitForAsync();
        var userRow = page.Locator("tbody tr").Filter(new LocatorFilterOptions { HasText = email });
        await userRow.WaitForAsync();
        (await userRow.Locator("td[data-label='Name']").InnerTextAsync()).Should().Be("Ada Tester");

        // Update
        await userRow.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Edit" }).ClickAsync();
        await page.FillAsync("input[name='lastName']", "Updated");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Update", Exact = true }).ClickAsync();
        await page.GetByText("User updated successfully!").WaitForAsync();
        await userRow.Locator("td[data-label='Name']:text-is('Ada Updated')").WaitForAsync();

        // Delete
        await userRow.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete" }).ClickAsync();
        await page.GetByText("User deleted successfully!").WaitForAsync();
        await userRow.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
        (await userRow.CountAsync()).Should().Be(0);
    }

    [Fact(Explicit = true)]
    public async Task Blazor_CreateUser_InvalidSubmit_ShowsEachServerErrorUnderItsField()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var page = await OpenBlazorUsersPageAsync(cancellationToken);
        await page.ClickAsync("button:has-text('Create User')");

        var inputs = page.Locator(".mud-dialog input");
        await inputs.Nth(1).FillAsync("A"); // First Name: too short
        await inputs.Nth(3).FillAsync("not-an-email"); // Email
        await inputs.Nth(16).FillAsync("X"); // Work address: City, too short
        await inputs.Nth(18).FillAsync("DNK"); // Work address: Country code (2 letters)

        // Act
        await page.ClickAsync(".mud-dialog button:has-text('Create')");
        await page.Locator(".mud-dialog .mud-input-error").First.WaitForAsync();

        // Assert - Gender, First Name, Last Name, Email, Work City, Work Country code (2 letters)
        var errorIndices = await page.EvaluateAsync<int[]>(BlazorErrorIndicesScript);
        errorIndices.Should().BeEquivalentTo([0, 1, 2, 3, 16, 18]);

        // Several messages for one field are shown one per line (Last Name: required, length, uppercase)
        var lastNameLines = (await page.Locator(".mud-dialog .mud-input-control").Nth(2).Locator(".mud-input-helper-text").InnerTextAsync())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        lastNameLines.Should().HaveCountGreaterThan(1).And.NotContain(line => line.Contains("; ", StringComparison.Ordinal));
    }

    [Fact(Explicit = true)]
    public async Task Blazor_EditUser_InvalidSubmit_ShowsEachServerErrorUnderItsField()
    {
        // Arrange - UpdateUserRequest renames WorkAddress to "address" ([JsonPropertyName]), so the keys are "address.*"
        var cancellationToken = TestContext.Current.CancellationToken;
        var page = await OpenBlazorUsersPageAsync(cancellationToken);

        // The seed data picks a random gender; "None" would add a Gender error, so edit a user with another gender.
        var userRow = page
            .Locator(".mud-table-body tr")
            .Filter(new LocatorFilterOptions { HasNot = page.Locator("td[data-label='Gender']:text-is('None')") })
            .First;
        await userRow.Locator("button").First.ClickAsync(); // Edit
        await page.GetByText("Edit User").WaitForAsync();

        var inputs = page.Locator(".mud-dialog input");
        await inputs.Nth(1).FillAsync("A"); // First Name: too short
        await inputs.Nth(3).FillAsync("not-an-email"); // Email
        await inputs.Nth(7).FillAsync("X"); // Address: City, too short
        await inputs.Nth(9).FillAsync("DNK"); // Address: Country code (2 letters)

        // Act
        await page.ClickAsync(".mud-dialog button:has-text('Update')");
        await page.Locator(".mud-dialog .mud-input-error").First.WaitForAsync();

        // Assert
        var errorIndices = await page.EvaluateAsync<int[]>(BlazorErrorIndicesScript);
        errorIndices.Should().BeEquivalentTo([1, 3, 7, 9]);

        // The validator targets the request model, so the display name is "First Name" (not "Request First Name")
        var firstNameHelperText = await page.Locator(".mud-dialog .mud-input-control").Nth(1).Locator(".mud-input-helper-text").InnerTextAsync();
        firstNameHelperText.Should().StartWith("'First Name' must be between 2 and 10 characters.");
    }

    [Fact(Explicit = true)]
    public async Task React_CreateUserWithExistingEmail_ShowsTheProblemDetail()
    {
        // Arrange - the API answers 409 with ProblemDetails; the UI shows its "detail"
        var cancellationToken = TestContext.Current.CancellationToken;
        var page = await OpenReactUsersPageAsync(cancellationToken);
        var existingEmail = await page.Locator("tbody tr").First.Locator("td[data-label='Email']").InnerTextAsync();

        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create User" }).ClickAsync();
        await page.Locator("[id='field-gender']").ClickAsync();
        await page.GetByRole(AriaRole.Option, new PageGetByRoleOptions { Name = "Female", Exact = true }).ClickAsync();
        await page.FillAsync("input[name='firstName']", "Ada");
        await page.FillAsync("input[name='lastName']", "Tester");
        await page.FillAsync("input[name='email']", existingEmail);

        // Act
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create", Exact = true }).ClickAsync();

        // Assert - exactly the detail text: no JSON, no quotes
        var alert = page.Locator(".MuiSnackbar-root .MuiAlert-message");
        await alert.WaitForAsync();
        (await alert.InnerTextAsync()).Should().Be($"A user already exists with the email '{existingEmail}'.");
    }

    [Fact(Explicit = true)]
    public async Task Blazor_CreateUserWithExistingEmail_ShowsTheProblemDetail()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var page = await OpenBlazorUsersPageAsync(cancellationToken);
        var existingEmail = (await page.Locator(".mud-table-body tr").First.Locator("td[data-label='Email']").InnerTextAsync()).Trim();

        await page.ClickAsync("button:has-text('Create User')");
        await page.Locator(".mud-dialog .mud-select").First.ClickAsync(); // Gender
        await page.Locator(".mud-popover-open .mud-list-item:has-text('Female')").ClickAsync();
        var inputs = page.Locator(".mud-dialog input");
        await inputs.Nth(1).FillAsync("Ada");
        await inputs.Nth(2).FillAsync("Tester");
        await inputs.Nth(3).FillAsync(existingEmail);
        await inputs.Nth(16).FillAsync("Aarhus"); // the Blazor form always sends a work address, so its city must be valid

        // Act
        await page.ClickAsync(".mud-dialog button:has-text('Create')");

        // Assert - exactly the detail text: no JSON
        var snackbar = page.Locator(".mud-snackbar-content-message");
        await snackbar.WaitForAsync();
        (await snackbar.InnerTextAsync()).Trim().Should().Be($"A user already exists with the email '{existingEmail}'.");
    }

    private async Task<IPage> OpenBlazorUsersPageAsync(
        CancellationToken cancellationToken)
    {
        var page = await fixture.NewPageAsync(cancellationToken);
        await page.GotoAsync(new Uri(fixture.WebUrl, "/users").ToString());
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        return page;
    }

    private async Task<IPage> OpenReactUsersPageAsync(
        CancellationToken cancellationToken)
    {
        var page = await fixture.NewPageAsync(cancellationToken);
        await page.GotoAsync(fixture.ReactUrl.ToString());
        await page.Locator("tbody tr").First.WaitForAsync(); // users loaded
        return page;
    }
}