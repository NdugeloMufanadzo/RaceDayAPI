using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos
{
    public class ResultDto
    {
        [Required]
        public TimeSpan FinishTime { get; set; }

        [Range(1, 100000)]
        public int? Position { get; set; }
    }

    public class ProfileDto
    {
        [Required, StringLength(100)]
        public string FullName { get; set; } = string.Empty;
    }
}