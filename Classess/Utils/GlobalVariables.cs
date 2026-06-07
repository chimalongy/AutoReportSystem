namespace ARS.Classess.Utils
{
    public static class GlobalVariables
    {
       public static string rootDrive = Path.GetPathRoot(AppContext.BaseDirectory);

       public static string rootDirectory = Path.Combine(rootDrive, "ARS");
       public static string reportsDirectory = Path.Combine(rootDirectory,"Reports");


    }
}
