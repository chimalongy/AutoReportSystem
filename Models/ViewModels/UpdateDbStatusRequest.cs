namespace ARS.Models.ViewModels
{
    /// <summary>
    /// Request to update the status (active/inactive) of a database configuration.
    /// </summary>
    public class UpdateDbStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }
}
