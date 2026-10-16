using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly RaceDayDbContext _db;

        public UsersController(RaceDayDbContext db)
        {
            _db = db;
        }

        private int CurrentUserId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>Returns the profile of the logged-in user.</summary>
        /// <response code="200">The profile.</response>
        /// <response code="401">Not logged in.</response>
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var userId = CurrentUserId;
            var user = await _db.Users.Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
                return NotFound(new { message = "User not found." });

            return Ok(new
            {
                user.UserId,
                user.FullName,
                user.Email,
                Role = user.Role.RoleName,
                user.CreatedAt
            });
        }

        /// <summary>Updates the logged-in user's own profile.</summary>
        /// <response code="200">Profile updated.</response>
        /// <response code="400">Invalid fields.</response>
        /// <response code="401">Not logged in.</response>
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMe(ProfileDto dto)
        {
            var userId = CurrentUserId;
            var user = await _db.Users.Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
                return NotFound(new { message = "User not found." });

            user.FullName = dto.FullName.Trim();
            await _db.SaveChangesAsync();

            return Ok(new
            {
                user.UserId,
                user.FullName,
                user.Email,
                Role = user.Role.RoleName,
                user.CreatedAt
            });
        }

        /// <summary>Returns a user's profile. Organisers only.</summary>
        /// <response code="200">The profile.</response>
        /// <response code="403">Participants cannot view other users.</response>
        /// <response code="404">User does not exist.</response>
        [Authorize(Roles = "Organiser")]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var user = await _db.Users.Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == id);
            if (user == null)
                return NotFound(new { message = "User not found." });

            return Ok(new
            {
                user.UserId,
                user.FullName,
                user.Email,
                Role = user.Role.RoleName
            });
        }
    }
}