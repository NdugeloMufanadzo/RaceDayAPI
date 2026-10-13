using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos
{
    public class CategoryDto
    {
        [Required, StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [Range(0, 120)]
        public int? MinAge { get; set; }

        [Range(0, 120)]
        public int? MaxAge { get; set; }

        [Range(0.01, 1000)]
        public decimal CategoryDistance { get; set; }
    }
}