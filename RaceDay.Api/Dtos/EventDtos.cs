using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos
{
    public class EventDto
    {
        [Required, StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        [Required]
        public DateTime EventDate { get; set; }

        [Required, StringLength(150)]
        public string Location { get; set; } = string.Empty;

        [Range(0.01, 1000)]
        public decimal Distance { get; set; }

        [Required, RegularExpression("^(Run|Walk|Cycle)$",
            ErrorMessage = "EventType must be Run, Walk or Cycle.")]
        public string EventType { get; set; } = string.Empty;
    }
}