using System.Threading.Tasks;

namespace ProjectRover.Services;

public class NullAnalyticsService : IAnalyticsService
{
    public void TrackEvent(AnalyticsEvent @event)
    {
    }

    public Task TrackEventAsync(AnalyticsEvent @event)
    {
        return Task.CompletedTask;
    }
}
