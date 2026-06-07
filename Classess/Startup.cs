using System.IO;
using ARS.Classess.Utils;

namespace ARS.Classess
{
    public static class Startup
    {
        public static void Initialize()
        {
            Directory.CreateDirectory(GlobalVariables.rootDirectory);
            Directory.CreateDirectory(GlobalVariables.reportsDirectory);


        }
    }
}