using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;
using System.Reflection;
using System.Security.Claims;
using Xunit;

namespace RaceDay.Tests
{
    public class ApiTests
    {
        // ---------- helpers ----------
        private static RaceDayDbContext NewDb()
        {
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var db = new RaceDayDbContext(options);
            db.Database.EnsureCreated();   // also seeds the two roles
            return db;
        }

        private static T WithUser<T>(T controller, int userId, string role) where T : ControllerBase
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
                }
            };
            return controller;
        }

        private static int Code(IActionResult r) =>
            ((IStatusCodeActionResult)r).StatusCode ?? 200;

        private static User AddUser(RaceDayDbContext db, string email, int roleId)
        {
            var h = PasswordHasher.HashPassword("Password123");
            var user = new User
            {
                FullName = "Test User",
                Email = email,
                PasswordHash = h.Hash,
                PasswordSalt = h.Salt,
                RoleId = roleId,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            db.SaveChanges();
            return user;
        }

        private static Event AddEvent(RaceDayDbContext db, int organiserId)
        {
            var ev = new Event
            {
                Name = "Test 10K",
                EventDate = DateTime.UtcNow.AddDays(30),
                Location = "Johannesburg",
                Distance = 10,
                EventType = "Run",
                OrganiserId = organiserId
            };
            db.Events.Add(ev);
            db.SaveChanges();
            return ev;
        }

        private static Category AddCategory(RaceDayDbContext db, int eventId)
        {
            var cat = new Category { EventId = eventId, Name = "Senior", CategoryDistance = 10 };
            db.Categories.Add(cat);
            db.SaveChanges();
            return cat;
        }

        private static AuthController NewAuth(RaceDayDbContext db)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = "RaceDay_Test_Secret_Key_Must_Be_Long_Enough_123!",
                    ["Jwt:Issuer"] = "RaceDayApi",
                    ["Jwt:Audience"] = "RaceDayClients"
                })
                .Build();
            return new AuthController(db, new TokenService(config));
        }

        private static EventDto NewEventDto() => new EventDto
        {
            Name = "City 10K",
            Description = "Road race",
            EventDate = DateTime.UtcNow.AddDays(30),
            Location = "Pretoria",
            Distance = 10,
            EventType = "Run"
        };

        // ---------- 1. Registration and login ----------
        [Fact]
        public async Task Register_ValidOrganiser_Returns201()
        {
            var db = NewDb();
            var result = await NewAuth(db).Register(new RegisterDto
            {
                FullName = "Org One",
                Email = "org@test.com",
                Password = "Password123",
                Role = "Organiser"
            });

            Assert.Equal(201, Code(result));
            Assert.Single(db.Users);
        }

        [Fact]
        public async Task Register_DuplicateEmail_Returns409()
        {
            var db = NewDb();
            var auth = NewAuth(db);
            var dto = new RegisterDto
            {
                FullName = "P One",
                Email = "p@test.com",
                Password = "Password123",
                Role = "Participant"
            };

            await auth.Register(dto);
            var second = await auth.Register(dto);

            Assert.Equal(409, Code(second));
        }

        [Fact]
        public async Task Login_CorrectPassword_Returns200()
        {
            var db = NewDb();
            AddUser(db, "login@test.com", 2);

            var result = await NewAuth(db).Login(new LoginDto
            {
                Email = "login@test.com",
                Password = "Password123"
            });

            Assert.Equal(200, Code(result));
        }

        [Fact]
        public async Task Login_WrongPassword_Returns401()
        {
            var db = NewDb();
            AddUser(db, "login@test.com", 2);

            var result = await NewAuth(db).Login(new LoginDto
            {
                Email = "login@test.com",
                Password = "WrongPassword"
            });

            Assert.Equal(401, Code(result));
        }

        // ---------- 2. Organiser event management ----------
        [Fact]
        public async Task Organiser_CreateEvent_Returns201()
        {
            var db = NewDb();
            var org = AddUser(db, "org@test.com", 1);
            var controller = WithUser(new EventsController(db), org.UserId, "Organiser");

            var result = await controller.Create(NewEventDto());

            Assert.Equal(201, Code(result));
            Assert.Single(db.Events);
        }

        [Fact]
        public async Task Organiser_UpdateOwnEvent_Returns200()
        {
            var db = NewDb();
            var org = AddUser(db, "org@test.com", 1);
            var ev = AddEvent(db, org.UserId);
            var controller = WithUser(new EventsController(db), org.UserId, "Organiser");

            var dto = NewEventDto();
            dto.Name = "Renamed Event";
            var result = await controller.Update(ev.EventId, dto);

            Assert.Equal(200, Code(result));
            Assert.Equal("Renamed Event", db.Events.Single().Name);
        }

        [Fact]
        public async Task Organiser_UpdateOtherOrganisersEvent_IsForbidden()
        {
            var db = NewDb();
            var owner = AddUser(db, "owner@test.com", 1);
            var other = AddUser(db, "other@test.com", 1);
            var ev = AddEvent(db, owner.UserId);
            var controller = WithUser(new EventsController(db), other.UserId, "Organiser");

            var result = await controller.Update(ev.EventId, NewEventDto());

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task Organiser_DeleteOwnEvent_Returns204()
        {
            var db = NewDb();
            var org = AddUser(db, "org@test.com", 1);
            var ev = AddEvent(db, org.UserId);
            var controller = WithUser(new EventsController(db), org.UserId, "Organiser");

            var result = await controller.Delete(ev.EventId);

            Assert.Equal(204, Code(result));
            Assert.Empty(db.Events);
        }

        // ---------- 3. Role rejection ----------
        [Fact]
        public void Role_CreateUpdateDeleteEvent_RequireOrganiser()
        {
            var methods = typeof(EventsController).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttributes<HttpPostAttribute>().Any()
                         || m.GetCustomAttributes<HttpPutAttribute>().Any()
                         || m.GetCustomAttributes<HttpDeleteAttribute>().Any())
                .ToList();

            Assert.NotEmpty(methods);
            foreach (var m in methods)
            {
                var auth = m.GetCustomAttribute<AuthorizeAttribute>();
                Assert.NotNull(auth);
                Assert.Equal("Organiser", auth!.Roles);
            }
        }

        [Fact]
        public void Role_Enrol_RequiresParticipant()
        {
            var method = typeof(EnrolmentsController).GetMethod("Enrol")!;
            var auth = method.GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(auth);
            Assert.Equal("Participant", auth!.Roles);
        }

        // ---------- 4. Participant enrolment ----------
        [Fact]
        public async Task Participant_Enrol_Returns201()
        {
            var db = NewDb();
            var org = AddUser(db, "org@test.com", 1);
            var part = AddUser(db, "part@test.com", 2);
            var cat = AddCategory(db, AddEvent(db, org.UserId).EventId);
            var controller = WithUser(new EnrolmentsController(db), part.UserId, "Participant");

            var result = await controller.Enrol(cat.CategoryId);

            Assert.Equal(201, Code(result));
            Assert.Single(db.Enrolments);
        }

        [Fact]
        public async Task Participant_EnrolTwice_Returns409()
        {
            var db = NewDb();
            var org = AddUser(db, "org@test.com", 1);
            var part = AddUser(db, "part@test.com", 2);
            var cat = AddCategory(db, AddEvent(db, org.UserId).EventId);
            var controller = WithUser(new EnrolmentsController(db), part.UserId, "Participant");

            await controller.Enrol(cat.CategoryId);
            var second = await controller.Enrol(cat.CategoryId);

            Assert.Equal(409, Code(second));
            Assert.Single(db.Enrolments);
        }

        [Fact]
        public async Task Participant_EnrolInMissingCategory_Returns404()
        {
            var db = NewDb();
            var part = AddUser(db, "part@test.com", 2);
            var controller = WithUser(new EnrolmentsController(db), part.UserId, "Participant");

            var result = await controller.Enrol(999);

            Assert.Equal(404, Code(result));
        }

        [Fact]
        public async Task Participant_WithdrawOtherUsersEnrolment_IsForbidden()
        {
            var db = NewDb();
            var org = AddUser(db, "org@test.com", 1);
            var owner = AddUser(db, "owner@test.com", 2);
            var intruder = AddUser(db, "intruder@test.com", 2);
            var ev = AddEvent(db, org.UserId);
            var cat = AddCategory(db, ev.EventId);
            var enrolment = new Enrolment
            {
                ParticipantId = owner.UserId,
                EventId = ev.EventId,
                CategoryId = cat.CategoryId,
                EnrolmentDate = DateTime.UtcNow
            };
            db.Enrolments.Add(enrolment);
            db.SaveChanges();

            var controller = WithUser(new EnrolmentsController(db), intruder.UserId, "Participant");
            var result = await controller.Withdraw(enrolment.EnrolmentId);

            Assert.IsType<ForbidResult>(result);
        }
    }
}