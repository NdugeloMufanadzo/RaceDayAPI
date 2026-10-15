using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers
{
    [ApiController]
    [Authorize]
    public class EnrolmentsController : ControllerBase
    {
        private readonly RaceDayDbContext _db;

        public EnrolmentsController(RaceDayDbContext db)
        {
            _db = db;
        }

        private int CurrentUserId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>Enrols the logged-in Participant in a category (and so in its event).</summary>
        /// <response code="201">Enrolment created.</response>
        /// <response code="403">Only Participants can enrol.</response>
        /// <response code="404">Category does not exist.</response>
        /// <response code="409">Already enrolled in this event.</response>
        [Authorize(Roles = "Participant")]
        [HttpPost("api/categories/{id}/enrol")]
        public async Task<IActionResult> Enrol(int id)
        {
            var userId = CurrentUserId;

            var category = await _db.Categories.FindAsync(id);
            if (category == null)
                return NotFound(new { message = "Category not found." });

            var already = await _db.Enrolments
                .AnyAsync(e => e.ParticipantId == userId && e.EventId == category.EventId);
            if (already)
                return Conflict(new { message = "You are already enrolled in this event." });

            var enrolment = new Enrolment
            {
                ParticipantId = userId,
                EventId = category.EventId,
                CategoryId = id,
                EnrolmentDate = DateTime.UtcNow
            };

            _db.Enrolments.Add(enrolment);
            await _db.SaveChangesAsync();

            return StatusCode(201, new
            {
                enrolment.EnrolmentId,
                enrolment.ParticipantId,
                enrolment.EventId,
                enrolment.CategoryId,
                enrolment.EnrolmentDate
            });
        }

        /// <summary>Lists the logged-in Participant's own enrolments.</summary>
        /// <response code="200">Array of enrolments.</response>
        [Authorize(Roles = "Participant")]
        [HttpGet("api/users/me/enrolments")]
        public async Task<IActionResult> MyEnrolments()
        {
            var userId = CurrentUserId;

            var list = await _db.Enrolments
                .Where(e => e.ParticipantId == userId)
                .Select(e => new
                {
                    e.EnrolmentId,
                    e.EventId,
                    EventName = e.Event.Name,
                    EventDate = e.Event.EventDate,
                    e.CategoryId,
                    CategoryName = e.Category.Name,
                    e.EnrolmentDate
                })
                .ToListAsync();

            return Ok(list);
        }

        /// <summary>Lists everyone enrolled in a category. Only the owning Organiser may view this.</summary>
        /// <response code="200">Array of enrolments.</response>
        /// <response code="403">You do not own this event.</response>
        /// <response code="404">Category does not exist.</response>
        [Authorize(Roles = "Organiser")]
        [HttpGet("api/categories/{id}/enrolments")]
        public async Task<IActionResult> ForCategory(int id)
        {
            var category = await _db.Categories.Include(c => c.Event)
                .FirstOrDefaultAsync(c => c.CategoryId == id);
            if (category == null)
                return NotFound(new { message = "Category not found." });
            if (category.Event.OrganiserId != CurrentUserId)
                return Forbid();

            var list = await _db.Enrolments
                .Where(e => e.CategoryId == id)
                .Select(e => new
                {
                    e.EnrolmentId,
                    e.ParticipantId,
                    ParticipantName = e.Participant.FullName,
                    e.EventId,
                    e.CategoryId,
                    e.EnrolmentDate
                })
                .ToListAsync();

            return Ok(list);
        }

        /// <summary>Lists everyone enrolled in an event. Only the owning Organiser may view this.</summary>
        /// <response code="200">Array of enrolments.</response>
        /// <response code="403">You do not own this event.</response>
        /// <response code="404">Event does not exist.</response>
        [Authorize(Roles = "Organiser")]
        [HttpGet("api/events/{id}/enrolments")]
        public async Task<IActionResult> ForEvent(int id)
        {
            var ev = await _db.Events.FindAsync(id);
            if (ev == null)
                return NotFound(new { message = "Event not found." });
            if (ev.OrganiserId != CurrentUserId)
                return Forbid();

            var list = await _db.Enrolments
                .Where(e => e.EventId == id)
                .Select(e => new
                {
                    e.EnrolmentId,
                    e.ParticipantId,
                    ParticipantName = e.Participant.FullName,
                    e.CategoryId,
                    CategoryName = e.Category.Name,
                    e.EnrolmentDate
                })
                .ToListAsync();

            return Ok(list);
        }

        /// <summary>Withdraws the logged-in Participant from an event.</summary>
        /// <response code="204">Withdrawn.</response>
        /// <response code="403">This is not your enrolment.</response>
        /// <response code="404">Enrolment does not exist.</response>
        /// <response code="409">A result is already recorded.</response>
        [Authorize(Roles = "Participant")]
        [HttpDelete("api/enrolments/{id}")]
        public async Task<IActionResult> Withdraw(int id)
        {
            var enrolment = await _db.Enrolments.Include(e => e.Result)
                .FirstOrDefaultAsync(e => e.EnrolmentId == id);
            if (enrolment == null)
                return NotFound(new { message = "Enrolment not found." });
            if (enrolment.ParticipantId != CurrentUserId)
                return Forbid();
            if (enrolment.Result != null)
                return Conflict(new { message = "A result is already recorded for this enrolment." });

            _db.Enrolments.Remove(enrolment);
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}