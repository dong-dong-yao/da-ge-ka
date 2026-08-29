namespace CheckInReminder;

public enum ReminderQueueAction
{
    Ignore,
    Show,
    Queue,
    ReplaceCurrent,
}

public sealed class ReminderQueue
{
    private bool breakIsPending;

    public ReminderKind? Active { get; private set; }

    public ReminderQueueAction Request(ReminderKind kind)
    {
        if (Active is null)
        {
            Active = kind;
            return ReminderQueueAction.Show;
        }

        if (kind == ReminderKind.Break)
        {
            breakIsPending = true;
            return ReminderQueueAction.Queue;
        }

        if (Active == ReminderKind.Break)
        {
            breakIsPending = true;
            Active = kind;
            return ReminderQueueAction.ReplaceCurrent;
        }

        return ReminderQueueAction.Ignore;
    }

    public ReminderKind? CompleteCurrent()
    {
        Active = null;
        if (!breakIsPending)
        {
            return null;
        }

        breakIsPending = false;
        Active = ReminderKind.Break;
        return ReminderKind.Break;
    }

    public void Clear()
    {
        Active = null;
        breakIsPending = false;
    }

    public void CancelPendingBreak() => breakIsPending = false;
}
