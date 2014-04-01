/// <summary>
/// An example of the least you need to do to implement a check
/// </summary>
public class DemoCheck : NrpeCheck
{
    public override NrpeMessage.NrpeResultState GetStatus(out string statusString, out Hashtable performanceData)
    {
        performanceData = new Hashtable();

        var demoMetric = 20;
        performanceData.Add("demo_metric", demoMetric);

        statusString = "Demo Metric: " + demoMetric.ToString();

        // Always Ok.
        return NrpeMessage.NrpeResultState.Ok;
    }
}
