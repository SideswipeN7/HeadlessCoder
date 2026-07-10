using System.Text.RegularExpressions;
using HeadlessCoder.Auth;

namespace HeadlessCoder.Tests;

public class TransformersPasswordTests
{
    [Fact]
    public void AllNames_AreUnique()
    {
        var names = TransformersPassword.AllNames;
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void AllNames_AreSingleAlphabeticWords()
    {
        Assert.All(TransformersPassword.AllNames, n => Assert.Matches("^[A-Za-z]+$", n));
    }

    [Fact]
    public void Generate_MatchesNameFollowedByThreeDigits()
    {
        for (int i = 0; i < 200; i++)
            Assert.Matches(@"^[A-Za-z]+\d{3}$", TransformersPassword.Generate());
    }

    [Fact]
    public void Generate_UsesAKnownNameAndAThreeDigitNumberInRange()
    {
        var known = TransformersPassword.AllNames.ToHashSet(StringComparer.Ordinal);

        for (int i = 0; i < 200; i++)
        {
            string pw = TransformersPassword.Generate();
            var m = Regex.Match(pw, @"^(?<name>[A-Za-z]+)(?<num>\d{3})$");
            Assert.True(m.Success, pw);
            Assert.Contains(m.Groups["name"].Value, known);

            int num = int.Parse(m.Groups["num"].Value);
            Assert.InRange(num, 100, 999);
        }
    }

    [Fact]
    public void Generate_ProducesSomeVariety()
    {
        var seen = new HashSet<string>();
        for (int i = 0; i < 50; i++)
            seen.Add(TransformersPassword.Generate());

        // Astronomically unlikely to collide into a single value across 50 draws.
        Assert.True(seen.Count > 1);
    }
}
