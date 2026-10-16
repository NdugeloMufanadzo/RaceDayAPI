using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly RaceDayDbContext _db;
        private readonly TokenService _tokens;

        public AuthController(RaceDayDbContext db, TokenService tokens)
        {
            _db = db;
            _tokens = tokens;
        }

        /// <summary>Creates a new account as an Organiser or a Participant.</summary>
        /// <response code="201">Account created.</response>
        /// <response code="400">Missing or invalid fields, or unknown role.</response>
        /// <response code="409">Email already registered.</response>
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto dto)
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == dto.Role);
            if (role == null)
                return BadRequest(new { message = "Role must be 'Organiser' or 'Participant'." });

            var email = dto.Email.Trim().ToLowerInvariant();
            if (await _db.Users.AnyAsync(u => u.Email == email))
                return Conflict(new { message = "Email already registered." });

            var (hash, salt) = PasswordHasher.HashPassword(dto.Password);

            var user = new User
            {
                FullName = dto.FullName.Trim(),
                Email = email,
                PasswordHash = hash,
                PasswordSalt = salt,
                RoleId = role.RoleId,
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return StatusCode(201, new { userId = user.UserId, role = role.RoleName });
        }

        /// <summary>Logs in with email and password and returns a JWT token.</summary>
        /// <response code="200">Login successful. Returns token, userId and role.</response>
        /// <response code="401">Invalid email or password.</response>
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            var email = dto.Email.Trim().ToLowerInvariant();
            var user = await _db.Users.Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null || !PasswordHasher.Verify(dto.Password, user.PasswordHash, user.PasswordSalt))
                return Unauthorized(new { message = "Invalid credentials." });

            var token = _tokens.CreateToken(user, user.Role.RoleName);
            return Ok(new { token, userId = user.UserId, role = user.Role.RoleName });
        }
    }
}