using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers
{
    [ApiController]
    public class ResultsController : ControllerBase
    {
        private readonly RaceDayDbContext _db;

        public ResultsController(RaceDayDbContext db)
        {
            _db = db;
        }

        private int CurrentUserId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>Records a finish time and position for an enrolment. Only the owning Organiser may do this.</summary>
        /// <response code="201">Result recorded.</response>
        /// <response code="400">Invalid finish time.</response>
        /// <response code="403">You do not own this event.</response>
        /// <response code="404">Enrolment does not exist.</response>
        /// <response code="409">A result already exists for this enrolment.</response>
        [Authorize(Roles = "Organiser")]
        [HttpPost("api/enrolments/{id}/result")]
        public async Task<IActionResult> Record(int id, ResultDto dto)
        {
            var enrolment = await _db.Enrolments.Include(e => e.Event)
                .FirstOrDefaultAsync(e => e.EnrolmentId == id);
            if (enrolment == null)
                return NotFound(new { message = "Enrolment not found." });
            if (enrolment.Event.OrganiserId != CurrentUserId)
                return Forbid();
            if (dto.FinishTime <= TimeSpan.Zero)
                return BadRequest(new { message = "FinishTime must be greater than zero." });
            if (await _db.Results.AnyAsync(r => r.EnrolmentId == id))
                return Conflict(new { message = "A result is already recorded for this enrolment." });

            var result = new Result
            {
                EnrolmentId = id,
                FinishTime = dto.FinishTime,
                FinishPosition = dto.Position,
                RecordedAt = DateTime.UtcNow
            };

            _db.Results.Add(result);
            await _db.SaveChangesAsync();

            return StatusCode(201, new
            {
                result.ResultId,
                result.EnrolmentId,
                result.FinishTime,
                result.FinishPosition,
                result.RecordedAt
            });
        }

        /// <summary>Returns the results leaderboard for a category, ordered by position.</summary>
        /// <response code="200">Array of results.</response>
        /// <response code="404">Category does not exist.</response>
        [HttpGet("api/categories/{id}/results")]
        public async Task<IActionResult> ForCategory(int id)
        {
            if (!await _db.Categories.AnyAsync(c => c.CategoryId == id))
                return NotFound(new { message = "Category not found." });

            var results = await _db.Results
                .Where(r => r.Enrolment.CategoryId == id)
                .OrderBy(r => r.FinishPosition == null)
                .ThenBy(r => r.FinishPosition)
                .ThenBy(r => r.FinishTime)
                .Select(r => new
                {
                    r.ResultId,
                    ParticipantName = r.Enrolment.Participant.FullName,
                    r.FinishTime,
                    r.FinishPosition
                })
                .ToListAsync();

            return Ok(results);
        }

        /// <summary>Returns the result for one enrolment. Visible to the Participant or the owning Organiser.</summary>
        /// <response code="200">The result.</response>
        /// <response code="403">Not your enrolment or event.</response>
        /// <response code="404">Enrolment or result does not exist.</response>
        [Authorize]
        [HttpGet("api/enrolments/{id}/result")]
        public async Task<IActionResult> ForEnrolment(int id)
        {
            var enrolment = await _db.Enrolments
                .Include(e => e.Event).Include(e => e.Result)
                .FirstOrDefaultAsync(e => e.EnrolmentId == id);
            if (enrolment == null)
                return NotFound(new { message = "Enrolment not found." });

            var userId = CurrentUserId;
            if (enrolment.ParticipantId != userId && enrolment.Event.OrganiserId != userId)
                return Forbid();

            if (enrolment.Result == null)
                return NotFound(new { message = "No result recorded yet." });

            return Ok(new
            {
                enrolment.Result.ResultId,
                enrolment.Result.EnrolmentId,
                enrolment.Result.FinishTime,
                enrolment.Result.FinishPosition
            });
        }

        /// <summary>Lists the logged-in Participant's own results.</summary>
        /// <response code="200">Array of results.</response>
        [Authorize(Roles = "Participant")]
        [HttpGet("api/users/me/results")]
        public async Task<IActionResult> MyResults()
        {
            var userId = CurrentUserId;

            var results = await _db.Results
                .Where(r => r.Enrolment.ParticipantId == userId)
                .Select(r => new
                {
                    r.ResultId,
                    r.EnrolmentId,
                    EventName = r.Enrolment.Event.Name,
                    CategoryName = r.Enrolment.Category.Name,
                    r.FinishTime,
                    r.FinishPosition
                })
                .ToListAsync();

            return Ok(results);
        }
    }
}