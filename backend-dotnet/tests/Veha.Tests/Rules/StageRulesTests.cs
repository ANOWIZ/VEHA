using Veha.Domain.Common;
using Veha.Domain.Enums;
using Veha.Domain.Rules;
using Xunit;

namespace Veha.Tests.Rules;

public class StageRulesTests
{
    [Fact]
    public void Next_and_prev()
    {
        Assert.Equal(Stage.Survey, StageRules.NextStage(Stage.Presale));
        Assert.Equal(Stage.Support, StageRules.NextStage(Stage.Pilot));
        Assert.Null(StageRules.NextStage(Stage.Closed));
        Assert.Equal(Stage.Presale, StageRules.PrevStage(Stage.Survey));
        Assert.Null(StageRules.PrevStage(Stage.Presale));
    }

    [Fact]
    public void Forward_transition_ok()
    {
        StageRules.ValidateTransition(Stage.Survey, Stage.Design, reason: null);
    }

    [Fact]
    public void Backward_requires_reason()
    {
        Assert.Throws<DomainValidationException>(() =>
            StageRules.ValidateTransition(Stage.Design, Stage.Survey, reason: null));
        StageRules.ValidateTransition(Stage.Design, Stage.Survey, reason: "пересмотр объёма");
    }

    [Fact]
    public void Same_or_skip_is_invalid()
    {
        Assert.Throws<DomainValidationException>(() =>
            StageRules.ValidateTransition(Stage.Design, Stage.Design, reason: null));
        Assert.Throws<DomainValidationException>(() =>
            StageRules.ValidateTransition(Stage.Presale, Stage.Design, reason: null)); // прыжок на 2
    }
}
