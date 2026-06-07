using System.ComponentModel.DataAnnotations.Schema;

namespace ARS.Models
{
    [Table("audit_logs", Schema = "public")]
    public class AuditLog
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("event")]
        public string? Event { get; set; }

        [Column("eventdate")]
        public DateTime? EventDate { get; set; }

        [Column("ipaddress")]
        public string? IpAddress { get; set; }

        [Column("pageurl")]
        public string? PageUrl { get; set; }

        [Column("userid")]
        public string? UserId { get; set; }
    }
}
