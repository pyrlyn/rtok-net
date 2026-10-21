namespace Rtok.Store.Model;

/// <summary>Table <c>task_counters</c>: the last task number under a parent.</summary>
public class TaskCounter
{
    public string Project { get; set; } = "";
    public string Parent { get; set; } = "";
    public long Last { get; set; }
}
