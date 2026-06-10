namespace ARS.Models.ViewModels
{
    // Add this class to your ViewModels or at the bottom of DashboardController.cs
    public class EncryptorRequest
    {
        public string Input { get; set; } = string.Empty;
        public bool? UseHashing { get; set; } = true;
    }
}
