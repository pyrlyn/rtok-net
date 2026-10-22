namespace Rtok.Tasks.Tests;

public sealed class TaskStatesTests
{
    [Theory]
    [InlineData("open", TaskState.Open)]
    [InlineData("TODO", TaskState.Open)]
    [InlineData("pending", TaskState.Open)]
    [InlineData("in_progress", TaskState.InProgress)]
    [InlineData(" In-Progress ", TaskState.InProgress)]
    [InlineData("completed", TaskState.Done)]
    [InlineData("done", TaskState.Done)]
    [InlineData("wontdo", TaskState.Closed)]
    [InlineData("won't-do", TaskState.Closed)]
    [InlineData("not_planned", TaskState.Closed)]
    public void Parses_states_and_aliases(string word, TaskState state) =>
        Assert.Equal(state, TaskStates.Parse(word));

    [Fact]
    public void Refuses_blocked()
    {
        var e = Assert.Throws<TaskException>(() => TaskStates.Parse("Blocked"));
        Assert.Equal(
            "unknown task status \"blocked\": use open, in-progress, done or closed",
            e.Message
        );
    }
}
