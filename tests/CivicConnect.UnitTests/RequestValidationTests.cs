using CivicConnect.Application.Contracts;
using CivicConnect.Application.Validation;
using CivicConnect.Domain.Entities;
using CivicConnect.Domain.Events;
using Xunit;

namespace CivicConnect.UnitTests;

public class RequestValidationTests
{
    private static RequestInput Valid(string title = "Broken pipe", string description = "Water leaking at intersection.",
        string location = "Main Street", int categoryId = 1) => new(title, description, location, categoryId);

    [Fact] public void UNIT01_Valid_request_has_no_validation_errors() =>
        Assert.Empty(RequestValidator.Validate(Valid()));

    [Theory]
    [InlineData("Abcd")]
    [InlineData("")]
    public void UNIT02_Invalid_short_or_empty_title_is_rejected(string title) =>
        Assert.Contains(RequestValidator.Validate(Valid(title: title)), x => x.Contains("Title"));

    [Fact] public void UNIT03_Title_at_minimum_boundary_is_accepted() =>
        Assert.Empty(RequestValidator.Validate(Valid(title: "12345")));

    [Fact] public void UNIT04_Title_above_maximum_boundary_is_rejected() =>
        Assert.Contains(RequestValidator.Validate(Valid(title: new string('X', 121))), x => x.Contains("Title"));

    [Fact] public void UNIT05_Short_description_is_rejected() =>
        Assert.Contains(RequestValidator.Validate(Valid(description: "tiny")), x => x.Contains("Description"));

    [Fact] public void UNIT06_Inactive_category_id_cannot_pass_first_stage_validation() =>
        Assert.Contains(RequestValidator.Validate(Valid(categoryId: 0)), x => x.Contains("category"));

    [Fact] public void UNIT07_Empty_location_is_rejected() =>
        Assert.Contains(RequestValidator.Validate(Valid(location: " ")), x => x.Contains("Location"));

    [Fact] public void UNIT08_Pending_to_in_progress_is_allowed() =>
        Assert.True(RequestWorkflow.CanTransition(RequestStatus.Pending, RequestStatus.InProgress));

    [Fact] public void UNIT09_Pending_directly_to_closed_is_rejected() =>
        Assert.False(RequestWorkflow.CanTransition(RequestStatus.Pending, RequestStatus.Closed));

    [Fact] public void UNIT10_Closed_request_cannot_be_modified() =>
        Assert.All(Enum.GetValues<RequestStatus>(), next =>
            Assert.False(RequestWorkflow.CanTransition(RequestStatus.Closed, next)));

    [Fact] public void UNIT11_Resolved_can_be_reopened_for_correction() =>
        Assert.True(RequestWorkflow.CanTransition(RequestStatus.Resolved, RequestStatus.InProgress));

    [Fact] public void UNIT12_Overlong_comment_is_rejected() =>
        Assert.NotNull(RequestValidator.ValidateComment(new string('a', 1501)));
}
