namespace Rtok.Measure;

/// <summary>One config's sums over its successful runs.</summary>
internal sealed class BenchRow
{
    public long Input { get; private set; }
    public long Cache { get; private set; }
    public long Output { get; private set; }
    public double Cost { get; private set; }
    public long Runs { get; private set; }
    public long Failed { get; private set; }
    public long Pass { get; set; }

    public void Add((long Input, long Cache, long Output, double Cost)? run)
    {
        if (run is not { } r)
        {
            Failed++;
            return;
        }
        Input += r.Input;
        Cache += r.Cache;
        Output += r.Output;
        Cost += r.Cost;
        Runs++;
    }
}
