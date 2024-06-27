using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System;

namespace GlobalIntegrationApi.Models
{
    [Table("IntegrationEventLog", Schema = "dbo")]
    public class IntegrationEventLog
    {
        [Key]
        public Guid EventId { get; set; }

        [Required, StringLength(int.MaxValue)]
        public string Content { get; set; }

        [Required]
        public DateTime CreationTime { get; set; }


        [Required, StringLength(int.MaxValue)]
        public string EventTypeName { get; set; }

        [Required]
        public int State { get; set; }

        [Required]
        public int TimesSent { get; set; }

        [StringLength(int.MaxValue)]
        public string? TransactionId { get; set; }
    }
}
