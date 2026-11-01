namespace SteamHourTracker
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--worker")
                return SteamWorker.Run(args);
            if (args.Length == 1 && args[0] == "--library")
                return SteamLibraryProcess.RunWorker();
            if (args.Length > 0 && args[0] == "--presence")
                return SteamPresenceProcess.RunWorker(args);
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
            return 0;
        }
    }
}
