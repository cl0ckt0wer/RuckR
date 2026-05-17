using System.ComponentModel.DataAnnotations;
using RuckR.Shared.Models;

namespace RuckR.Tests.ComponentTests;

/// <summary>
/// Tests the validation attributes on the PitchFormModel used by
/// PitchCreate.razor's EditForm.
/// </summary>
public class PitchCreateValidationTests
{
    /// <summary>
    /// Verifies pitch Form Model Name Is Required.
    /// </summary>
    [Fact]
    public void PitchFormModel_NameIsRequired()
    {
        var model = new PitchFormModel { Name = "", Latitude = 51.5074, Longitude = -0.1278, Type = "Standard" };
        var results = ValidateModel(model);

        Assert.Contains(results, r => r.MemberNames.Contains("Name"));
    }

    /// <summary>
    /// Verifies pitch Form Model Latitude Must Be In Range.
    /// </summary>
    [Fact]
    public void PitchFormModel_LatitudeMustBeInRange()
    {
        var model = new PitchFormModel { Name = "Test", Latitude = 100, Longitude = -0.1278, Type = "Standard" };
        var results = ValidateModel(model);

        Assert.Contains(results, r => r.MemberNames.Contains("Latitude"));
    }

    /// <summary>
    /// Verifies pitch Form Model Longitude Must Be In Range.
    /// </summary>
    [Fact]
    public void PitchFormModel_LongitudeMustBeInRange()
    {
        var model = new PitchFormModel { Name = "Test", Latitude = 51.5074, Longitude = 200, Type = "Standard" };
        var results = ValidateModel(model);

        Assert.Contains(results, r => r.MemberNames.Contains("Longitude"));
    }

    /// <summary>
    /// Verifies pitch Form Model Valid Data No Errors.
    /// </summary>
    [Fact]
    public void PitchFormModel_ValidData_NoErrors()
    {
        var model = new PitchFormModel
        {
            Name = "Twickenham",
            Latitude = 51.4564,
            Longitude = -0.3416,
            Type = "Stadium"
        };
        var results = ValidateModel(model);

        Assert.Empty(results);
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model);
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}


