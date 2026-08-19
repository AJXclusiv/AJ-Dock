namespace AJDock.App.ViewModels;

public sealed class CalendarDayViewModel
{
    public int Day { get; init; }
    public bool IsCurrentMonth { get; init; }
    public bool IsToday { get; init; }
}
