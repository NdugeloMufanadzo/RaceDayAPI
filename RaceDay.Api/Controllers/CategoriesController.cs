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
    public class CategoriesController : ControllerBase
    {
        private readonly RaceDayDbContext _db;

        public CategoriesController(RaceDayDbContext db)
        {
            _db = db;
        }

        private int CurrentUserId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>Lists all categories for an event.</summary>
        /// <response code="200">Array of categories.</response>
        /// <response code="404">Event does not exist.</response>
        [HttpGet("api/events/{eventId}/categories")]
        public async Task<IActionResult> GetForEvent(int eventId)
        {
            if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
                return NotFound(new { message = "Event not found." });

            var categories = await _db.Categories
                .Where(c => c.EventId == eventId)
                .Select(c => new
                {
                    c.CategoryId,
                    c.EventId,
                    c.Name,
                    c.MinAge,
                    c.MaxAge,
                    c.CategoryDistance
                })
                .ToListAsync();

            return Ok(categories);
        }

        /// <summary>Adds a category to an event. Only the owning Organiser may do this.</summary>
        /// <response code="201">Category created.</response>
        /// <response code="400">Invalid fields or MinAge greater than MaxAge.</response>
        /// <response code="403">You do not own this event.</response>
        /// <response code="404">Event does not exist.</response>
        [Authorize(Roles = "Organiser")]
        [HttpPost("api/events/{eventId}/categories")]
        public async Task<IActionResult> Create(int eventId, CategoryDto dto)
        {
            if (dto.MinAge.HasValue && dto.MaxAge.HasValue && dto.MinAge > dto.MaxAge)
                return BadRequest(new { message = "MinAge cannot be greater than MaxAge." });

            var ev = await _db.Events.FindAsync(eventId);
            if (ev == null)
                return NotFound(new { message = "Event not found." });
            if (ev.OrganiserId != CurrentUserId)
                return Forbid();

            var category = new Category
            {
                EventId = eventId,
                Name = dto.Name,
                MinAge = dto.MinAge,
                MaxAge = dto.MaxAge,
                CategoryDistance = dto.CategoryDistance
            };

            _db.Categories.Add(category);
            await _db.SaveChangesAsync();

            return StatusCode(201, new
            {
                category.CategoryId,
                category.EventId,
                category.Name,
                category.MinAge,
                category.MaxAge,
                category.CategoryDistance
            });
        }

        /// <summary>Updates a category. Only the owning Organiser may do this.</summary>
        /// <response code="200">Category updated.</response>
        /// <response code="400">Invalid fields or MinAge greater than MaxAge.</response>
        /// <response code="403">You do not own this event.</response>
        /// <response code="404">Category does not exist.</response>
        [Authorize(Roles = "Organiser")]
        [HttpPut("api/categories/{id}")]
        public async Task<IActionResult> Update(int id, CategoryDto dto)
        {
            if (dto.MinAge.HasValue && dto.MaxAge.HasValue && dto.MinAge > dto.MaxAge)
                return BadRequest(new { message = "MinAge cannot be greater than MaxAge." });

            var category = await _db.Categories.Include(c => c.Event)
                .FirstOrDefaultAsync(c => c.CategoryId == id);
            if (category == null)
                return NotFound(new { message = "Category not found." });
            if (category.Event.OrganiserId != CurrentUserId)
                return Forbid();

            category.Name = dto.Name;
            category.MinAge = dto.MinAge;
            category.MaxAge = dto.MaxAge;
            category.CategoryDistance = dto.CategoryDistance;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                category.CategoryId,
                category.EventId,
                category.Name,
                category.MinAge,
                category.MaxAge,
                category.CategoryDistance
            });
        }

        /// <summary>Deletes a category. Only the owning Organiser may do this.</summary>
        /// <response code="204">Category deleted.</response>
        /// <response code="403">You do not own this event.</response>
        /// <response code="404">Category does not exist.</response>
        /// <response code="409">Category has enrolments and cannot be deleted.</response>
        [Authorize(Roles = "Organiser")]
        [HttpDelete("api/categories/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _db.Categories.Include(c => c.Event)
                .FirstOrDefaultAsync(c => c.CategoryId == id);
            if (category == null)
                return NotFound(new { message = "Category not found." });
            if (category.Event.OrganiserId != CurrentUserId)
                return Forbid();

            if (await _db.Enrolments.AnyAsync(en => en.CategoryId == id))
                return Conflict(new { message = "Category has enrolments and cannot be deleted." });

            _db.Categories.Remove(category);
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}