using Veha.Domain.Common;
using Veha.Domain.Rules;
using Xunit;

namespace Veha.Tests.Rules;

public class RiskRulesTests
{
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 2, 2)]
    [InlineData(1, 3, 3)]
    [InlineData(2, 2, 4)]
    [InlineData(2, 3, 6)]
    [InlineData(3, 2, 6)]
    [InlineData(3, 3, 9)]
    public void ComputeScore(int probability, int impact, int expected) =>
        Assert.Equal(expected, RiskRules.ComputeScore(probability, impact));

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    [InlineData(10)]
    public void ComputeScore_rejects_out_of_scale(int bad)
    {
        Assert.Throws<DomainValidationException>(() => RiskRules.ComputeScore(bad, 2));
        Assert.Throws<DomainValidationException>(() => RiskRules.ComputeScore(2, bad));
    }

    [Theory]
    [InlineData(1, "low")]
    [InlineData(2, "low")]
    [InlineData(3, "medium")]
    [InlineData(4, "medium")]
    [InlineData(6, "high")]
    [InlineData(9, "high")]
    public void RiskLevel_banding(int score, string level) =>
        Assert.Equal(level, RiskRules.RiskLevel(score));

    [Fact]
    public void Every_matrix_cell_has_valid_level()
    {
        for (var p = 1; p <= 3; p++)
            for (var i = 1; i <= 3; i++)
                Assert.Contains(RiskRules.RiskLevel(RiskRules.ComputeScore(p, i)),
                    new[] { "low", "medium", "high" });
    }
}
