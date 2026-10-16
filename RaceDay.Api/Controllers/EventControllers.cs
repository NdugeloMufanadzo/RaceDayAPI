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
    [Route("api/events")]
    public class EventsController : ControllerBase
    {
        private readonly RaceDayDbContext _db;

        public EventsController(RaceDayDbContext db)
        {
            _db = db;
        }

        private int CurrentUserId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>Lists all events, optionally filtered by location or a start date.</summary>
        /// <response code="200">Array of events.</response>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? location, [FromQuery] DateTime? from)
        {
            var query = _db.Events.AsQueryable();

            if (!string.IsNullOrWhiteSpace(location))
                query = query.Where(e => e.Location.Contains(location));
            if (from.HasValue)
                query = query.Where(e => e.EventDate >= from.Value);

            var events = await query
                .OrderBy(e => e.EventDate)
                .Select(e => new
                {
                    e.EventId,
                    e.Name,
                    e.Description,
                    e.EventDate,
                    e.Location,
                    e.Distance,
                    e.EventType,
                    e.OrganiserId
                })
                .ToListAsync();

            return Ok(events);
        }

        /// <summary>Returns one event with its categories.</summary>
        /// <response code="200">The event details.</response>
        /// <response code="404">Event does not exist.</response>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var ev = await _db.Events
                .Where(e => e.EventId == id)
                .Select(e => new
                {
                    e.EventId,
                    e.Name,
                    e.Description,
                    e.EventDate,
                    e.Location,
                    e.Distance,
                    e.EventType,
                    e.OrganiserId,
                    Categories = e.Categories.Select(c => new
                    {
                        c.CategoryId,
                        c.Name,
                        c.MinAge,
                        c.MaxAge,
                        c.CategoryDistance
                    })
                })
                .FirstOrDefaultAsync();

            if (ev == null)
                return NotFound(new { message = "Event not found." });

            return Ok(ev);
        }

        /// <summary>Creates a new event owned by the logged-in Organiser.</summary>
        /// <response code="201">Event created.</response>
        /// <response code="400">Invalid or missing fields.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">Logged in as a Participant.</response>
        [Authorize(Roles = "Organiser")]
        [HttpPost]
        public async Task<IActionResult> Create(EventDto dto)
        {
            var ev = new Event
            {
                Name = dto.Name,
                Description = dto.Description,
                EventDate = dto.EventDate,
                Location = dto.Location,
                Distance = dto.Distance,
                EventType = dto.EventType,
                OrganiserId = CurrentUserId
            };

            _db.Events.Add(ev);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = ev.EventId }, new
            {
                ev.EventId,
                ev.Name,
                ev.Description,
                ev.EventDate,
                ev.Location,
                ev.Distance,
                ev.EventType,
                ev.OrganiserId
            });
        }

        /// <summary>Updates an event. Only the Organiser who owns it may edit it.</summary>
        /// <response code="200">Event updated.</response>
        /// <response code="403">You do not own this event.</response>
        /// <response code="404">Event does not exist.</response>
        [Authorize(Roles = "Organiser")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, EventDto dto)
        {
            var ev = await _db.Events.FindAsync(id);
            if (ev == null)
                return NotFound(new { message = "Event not found." });
            if (ev.OrganiserId != CurrentUserId)
                return Forbid();

            ev.Name = dto.Name;
            ev.Description = dto.Description;
            ev.EventDate = dto.EventDate;
            ev.Location = dto.Location;
            ev.Distance = dto.Distance;
            ev.EventType = dto.EventType;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                ev.EventId,
                ev.Name,
                ev.Description,
                ev.EventDate,
                ev.Location,
                ev.Distance,
                ev.EventType,
                ev.OrganiserId
            });
        }

        /// <summary>Deletes an event and its categories. Only the owning Organiser may delete it.</summary>
        /// <response code="204">Event deleted.</response>
        /// <response code="403">You do not own this event.</response>
        /// <response code="404">Event does not exist.</response>
        /// <response code="409">Event has enrolments and cannot be deleted.</response>
        [Authorize(Roles = "Organiser")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var ev = await _db.Events.FindAsync(id);
            if (ev == null)
                return NotFound(new { message = "Event not found." });
            if (ev.OrganiserId != CurrentUserId)
                return Forbid();

            if (await _db.Enrolments.AnyAsync(en => en.EventId == id))
                return Conflict(new { message = "Event has enrolments and cannot be deleted." });

            _db.Events.Remove(ev);
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}