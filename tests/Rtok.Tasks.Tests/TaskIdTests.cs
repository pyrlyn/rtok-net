using System.Text.Json;
using CsCheck;

namespace Rtok.Tasks.Tests;

public sealed class TaskIdTests
{
    [Theory]
    [InlineData("R12", "R12")]
    [InlineData("r2.1", "R2.1")]
    [InlineData(" at7 ", "AT7")]
    [InlineData("Rtok1.20", "RTOK1.20")]
    [InlineData("ABCDEFGH4294967295", "ABCDEFGH4294967295")]
    public void Parses_any_case_and_prints_upper_case(string text, string printed) =>
        Assert.Equal(printed, TaskId.Parse(text).ToString());

    [Theory]
    [InlineData("", "has no number")]
    [InlineData("R", "task id \"R\" has no number")]
    [InlineData("12", "task id \"12\": task prefix \"\" must be 1 to 8 ASCII letters")]
    [InlineData("R0", "task id \"R0\": task numbers start at 1")]
    [InlineData("R012", "task id \"R012\": \"012\" is not a number from 1")]
    [InlineData("R1.", "\"\" is not a number from 1")]
    [InlineData("R1..2", "\"\" is not a number from 1")]
    [InlineData("R1.2.3", "task id \"R1.2.3\": task id needs 1 to 2 numbers, got 3")]
    [InlineData("R-1", "\"-1\" is not a number from 1")]
    [InlineData("R1a", "\"1a\" is not a number from 1")]
    [InlineData("Ж1", "is not a number from 1")]
    [InlineData("ABCDEFGHI1", "task prefix \"ABCDEFGHI\" must be 1 to 8 ASCII letters")]
    [InlineData("R99999999999", "\"99999999999\" is not a number from 1")]
    public void Refuses_what_is_not_an_id(string text, string message)
    {
        var e = Assert.Throws<TaskException>(() => TaskId.Parse(text));
        Assert.Contains(message, e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Orders_by_prefix_then_numerically()
    {
        string[] ids = ["R10", "R2.1", "Q1", "R9", "R2"];
        Assert.Equal("Q1 R2 R2.1 R9 R10", string.Join(' ', ids.Select(TaskId.Parse).Order()));
    }

    [Fact]
    public void Parent_drops_the_last_number_and_child_appends_one()
    {
        var id = TaskId.Parse("R2.1");
        Assert.Equal(TaskId.Parse("R2"), id.Parent);
        Assert.Null(TaskId.Parse("R2").Parent);
        Assert.Equal(id, TaskId.Parse("R2").Child(1));
        Assert.Throws<TaskException>(() => id.Child(1));
    }

    [Fact]
    public void Is_a_json_string()
    {
        Assert.Equal(
            "\"R2.1\"",
            JsonSerializer.Serialize(TaskId.Parse("r2.1"), TaskIdJson.Default.TaskId)
        );
        Assert.Equal(
            TaskId.Parse("A7"),
            JsonSerializer.Deserialize("\"a7\"", TaskIdJson.Default.TaskId)
        );
    }

    [Fact]
    public void Printing_and_parsing_round_trip() =>
        Gen.Select(
                Gen.String[Gen.Char['A', 'Z'], 1, 8],
                Gen.UInt[1, uint.MaxValue],
                Gen.UInt[0, 50]
            )
            .Sample(
                (prefix, n, sub) =>
                {
                    var id = sub == 0 ? TaskId.Of(prefix, n) : TaskId.Of(prefix, n, sub);
                    var back = TaskId.Parse(id.ToString().ToLowerInvariant());
                    Assert.Equal(id, back);
                    Assert.Equal(0, id.CompareTo(back));
                }
            );
}
