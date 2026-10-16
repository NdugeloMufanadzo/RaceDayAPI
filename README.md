# RaceDay API

**Module:** PROG6121 | **Student:** Ndugelo(st10478739)

RaceDay is a RESTful ASP.NET Core Web API (C#, .NET 8) for managing running, walking and cycling events. Organisers create events and categories and record results. Participants enrol in events and view their results. It uses Entity Framework Core (Code-First) with SQL Server.

**Video demo:** https://youtu.be/vi2XRMU9pbk
## Build status

![CI green build](ci-green.png)

## The two roles

| Role | What they can do |
|---|---|
| **Organiser** | Create, update and delete their own events and categories. View enrolments for their events. Record results. View user profiles. |
| **Participant** | Browse events and categories. Enrol in a category and withdraw. View their own enrolments and results. |

Role-based access is enforced in the API using JWT bearer tokens and `[Authorize(Roles = "...")]`. Ownership is also checked, so an Organiser cannot change another Organiser's event. Passwords are hashed with PBKDF2 (SHA-256, 100,000 iterations, random salt) and are never stored in plain text.

## Setup and run instructions

**Requirements:** Visual Studio 2022, .NET 8 SDK, SQL Server (Express is fine) and SQL Server Management Studio (optional).

1. Clone the repo: `git clone https://github.com/NdugeloMufanadzo/RaceDayAPI.git`
2. Open `RaceDay.sln` in Visual Studio 2022.
3. Open `RaceDay.Api/appsettings.json` and change the `Server=` part of the connection string to your own SQL Server instance, for example `Server=.\\SQLEXPRESS;Database=RaceDayDb;Trusted_Connection=True;TrustServerCertificate=True`.
4. Open **Tools → NuGet Package Manager → Package Manager Console**. Set **Default project** to `RaceDay.Api` and run: `Update-Database`. This creates the `RaceDayDb` database and seeds the two roles. If the database already exists from Part 1, delete it first.
5. Set `RaceDay.Api` as the startup project and press **F5**. Swagger opens in the browser.

### Trying it in Swagger
1. `POST /api/auth/register` to create an Organiser (`"role": "Organiser"`) and a Participant (`"role": "Participant"`).
2. `POST /api/auth/login`, then copy the `token` from the response.
3. Click **Authorize**, paste the token only (no word "Bearer") and click **Authorize**.
4. Use **Try it out** on any endpoint. Log out and log in as the other role to see role restrictions (403 Forbidden).

## Endpoints

| Area | Endpoint | Access |
|---|---|---|
| Auth | `POST /api/auth/register`, `POST /api/auth/login` | Public |
| Profile | `GET /api/users/me`, `PUT /api/users/me` | Logged in |
| Profile | `GET /api/users/{id}` | Organiser |
| Events | `GET /api/events`, `GET /api/events/{id}` | Public |
| Events | `POST /api/events`, `PUT /api/events/{id}`, `DELETE /api/events/{id}` | Organiser (owner) |
| Categories | `GET /api/events/{eventId}/categories` | Public |
| Categories | `POST /api/events/{eventId}/categories`, `PUT /api/categories/{id}`, `DELETE /api/categories/{id}` | Organiser (owner) |
| Enrolments | `POST /api/categories/{id}/enrol`, `GET /api/users/me/enrolments`, `DELETE /api/enrolments/{id}` | Participant |
| Enrolments | `GET /api/categories/{id}/enrolments`, `GET /api/events/{id}/enrolments` | Organiser (owner) |
| Results | `POST /api/enrolments/{id}/result` | Organiser (owner) |
| Results | `GET /api/categories/{id}/results` | Public |
| Results | `GET /api/enrolments/{id}/result` | Participant or owning Organiser |
| Results | `GET /api/users/me/results` | Participant |

Every endpoint has a description in Swagger.

## Unit tests

The `RaceDay.Tests` project (xUnit + EF Core InMemory) has 14 tests covering registration and login, Organiser event management, role rejection and Participant enrolment, with success and failure cases. Run them in Visual Studio with **Test → Run All Tests**, or with `dotnet test`.

## Continuous integration

GitHub Actions (`.github/workflows/ci.yml`) restores, builds and runs all tests on every push. The green build screenshot is shown above.

## Design notes and deviations from Part 1

- EF Core Code-First creates `nvarchar` columns, while my Part 1 SQL script used `varchar`. The tables and relationships are otherwise the same.
- Part 1 mentions session management. I used stateless JWT tokens (2-hour expiry) instead.
- Some optional fields in my Part 1 endpoint plan (for example contact phone and maximum participants) are not in my final ERD, so they are not implemented.
- Enrolment foreign keys use `DeleteBehavior.Restrict`, so an event or category with enrolments cannot be deleted (returns 409).

## AI use disclosure

I used an AI assistant (Claude by Anthropic) for guidance, code suggestions and debugging while building this project. I reviewed, tested and understand the code in this repository, and I can explain it.
