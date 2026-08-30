using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KaijensonIventory_SalesMotorShopWeb.Models
{
    public class Service
    {
        [Key]
        public int ServiceId { get; set; }

        [Required, StringLength(150)]
        [Display(Name = "Service Name")]
        public string ServiceName { get; set; } = string.Empty;

        [Range(0, 999999.99)]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Service Price")]
        public decimal ServicePrice { get; set; }

        // Optional: a service is created with just a name and price.
        // Categories remain required for products only.
        [Display(Name = "Category")]
        public int? CategoryId { get; set; }

        // Navigation to Category
        public Category? Category { get; set; }

        // Service description
        [StringLength(500)]
        [Display(Name = "Description")]
        public string? Description { get; set; }

        // Duration in minutes
        [Range(1, 1440)]
        public int DurationMinutes { get; set; }

        // Service status: Active or Inactive
        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Active";

        // Navigation to creator staff
        [ForeignKey("CreatedBy")]
        public Staff? CreatedByStaff { get; set; }

    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedBy { get; set; }
}
}

