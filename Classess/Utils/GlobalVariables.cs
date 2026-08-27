namespace ARS.Classess.Utils
{
    public static class GlobalVariables
    {
        public static string rootDrive => Path.GetPathRoot(AppContext.BaseDirectory);

        public static string rootDirectory => Path.Combine(rootDrive, "ARS");

        // Base reports directory (no date) - still available if needed elsewhere
        public static string reportsDirectoryBase => Path.Combine(rootDirectory, "Reports");

        // Dynamic, date-based reports directory - recalculated on every access
        public static string reportsDirectory =>
            Path.Combine(
                reportsDirectoryBase,
                DateTime.Now.Year.ToString(),
                DateTime.Now.ToString("MMMM")     // "August"
                //DateTime.Now.Day.ToString("D2")     // "27" (zero-padded)
            );
    }
}