namespace CheckInReminder;

static class Program
{
    private const string MutexName = @"Local\CheckInReminder.SingleInstance";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        AnimationCatalog.RefreshCustomCharacters();
        Application.Run(new ReminderApplicationContext());
    }
}
