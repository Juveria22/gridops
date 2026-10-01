using GridOps.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace GridOps.Api.Data.Seeding;

// dev/benchmark data. fixed random seed -> same data every run
public class DevDataSeeder(GridOpsDbContext db, ILogger<DevDataSeeder> logger)
{
    private const int BatchSize = 2000;

    private readonly Random _rng = new(42);
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    private static readonly Dictionary<Borough, string[]> Neighborhoods = new()
    {
        [Borough.Manhattan] = ["Harlem", "Upper West Side", "Upper East Side", "Midtown", "Chelsea", "Greenwich Village",
            "Lower East Side", "Financial District", "Washington Heights", "Inwood", "East Village", "Tribeca"],
        [Borough.Brooklyn] = ["Williamsburg", "Park Slope", "Bushwick", "Bedford-Stuyvesant", "Crown Heights", "Flatbush",
            "Sunset Park", "Bay Ridge", "Coney Island", "DUMBO", "Canarsie", "Greenpoint"],
        [Borough.Queens] = ["Astoria", "Long Island City", "Flushing", "Jamaica", "Jackson Heights", "Forest Hills",
            "Elmhurst", "Ridgewood", "Bayside", "Far Rockaway", "Corona", "Sunnyside"],
        [Borough.Bronx] = ["Riverdale", "Fordham", "Mott Haven", "Pelham Bay", "Throgs Neck", "Kingsbridge",
            "Morris Park", "Hunts Point", "Co-op City", "Soundview"],
        [Borough.StatenIsland] = ["St. George", "Tottenville", "Great Kills", "New Dorp", "Port Richmond", "Stapleton",
            "Annadale", "Westerleigh"],
    };

    // roughly population share
    private static readonly (Borough Borough, double Weight)[] BoroughWeights =
    [
        (Borough.Brooklyn, 0.31), (Borough.Queens, 0.27), (Borough.Manhattan, 0.19),
        (Borough.Bronx, 0.17), (Borough.StatenIsland, 0.06),
    ];

    private static readonly (string Cause, string Repair)[] Causes =
    [
        ("Underground cable failure", "Splice underground cable"),
        ("Transformer failure", "Replace transformer"),
        ("Manhole fire", "Inspect and repair manhole"),
        ("Network protector fault", "Replace network protector"),
        ("Feeder outage", "Repair feeder"),
        ("Tree contact with overhead lines", "Clear vegetation and repair lines"),
        ("Vehicle struck utility pole", "Replace pole"),
        ("Heat-related equipment overload", "Replace overloaded equipment"),
        ("Flooded vault", "Pump out vault and dry equipment"),
        ("Service cable fault", "Replace service cable"),
    ];

    private static readonly string[] FirstNames = ["Maria", "James", "Aisha", "Luis", "Priya", "Michael", "Fatima", "David",
        "Keisha", "Anthony", "Mei", "Joseph", "Sofia", "Kevin", "Nadia", "Carlos", "Grace", "Omar", "Rachel", "Daniel"];

    private static readonly string[] LastNames = ["Rodriguez", "Johnson", "Khan", "Chen", "Williams", "Patel", "Murphy",
        "Garcia", "Kim", "Brown", "Ali", "Rossi", "Nguyen", "Cohen", "Okafor", "Lopez", "Singh", "Davis", "Torres", "Lee"];

    public async Task SeedAsync(int outageCount)
    {
        if (await db.Outages.AnyAsync())
        {
            logger.LogWarning("Outages table not empty, skipping seed");
            return;
        }

        // bulk insert - skip per-entity change detection
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        var crews = CreateCrews();
        db.Crews.AddRange(crews);
        db.Users.AddRange(CreateUsers(crews));
        await db.SaveChangesAsync();

        var crewsByBorough = crews.GroupBy(c => c.HomeBorough).ToDictionary(g => g.Key, g => g.ToArray());
        var allCrews = crews.ToArray();

        for (var done = 0; done < outageCount; done += BatchSize)
        {
            var batch = Math.Min(BatchSize, outageCount - done);
            for (var i = 0; i < batch; i++)
                db.Outages.Add(CreateOutage(crewsByBorough, allCrews));

            await db.SaveChangesAsync();
            db.ChangeTracker.Clear(); // keep memory flat on big runs
            logger.LogInformation("Seeded {Count}/{Total} outages", done + batch, outageCount);
        }
    }

    private List<Crew> CreateCrews()
    {
        string[] types = ["Overhead", "Underground", "Network"];
        var crews = new List<Crew>();

        foreach (var borough in Enum.GetValues<Borough>())
        {
            var perBorough = borough == Borough.StatenIsland ? 3 : 6;
            for (var i = 0; i < perBorough; i++)
            {
                crews.Add(new Crew
                {
                    Name = $"{DisplayName(borough)} {types[i % types.Length]} {i / types.Length + 1}",
                    HomeBorough = borough,
                    CreatedAt = _now.AddYears(-2),
                });
            }
        }

        return crews;
    }

    private List<User> CreateUsers(List<Crew> crews)
    {
        var emails = new HashSet<string>();
        var users = new List<User>();

        for (var i = 0; i < 4; i++)
            users.Add(CreateUser(UserRole.Dispatcher, null, emails));

        foreach (var crew in crews)
        {
            users.Add(CreateUser(UserRole.Crew, crew, emails));
            users.Add(CreateUser(UserRole.Crew, crew, emails));
        }

        return users;
    }

    private User CreateUser(UserRole role, Crew? crew, HashSet<string> emails)
    {
        var first = Pick(FirstNames);
        var last = Pick(LastNames);

        var email = $"{first}.{last}@gridops.example.com".ToLowerInvariant();
        for (var n = 2; !emails.Add(email); n++)
            email = $"{first}.{last}{n}@gridops.example.com".ToLowerInvariant();

        return new User
        {
            Email = email,
            DisplayName = $"{first} {last}",
            Role = role,
            Crew = crew,
            CreatedAt = _now.AddYears(-2),
        };
    }

    private Outage CreateOutage(Dictionary<Borough, Crew[]> crewsByBorough, Crew[] allCrews)
    {
        var borough = PickBorough();
        var neighborhood = Pick(Neighborhoods[borough]);
        var (cause, repair) = Pick(Causes);

        // ~15% in last week so the dashboard has active work, rest spread over 18 months
        var recent = _rng.NextDouble() < 0.15;
        var reportedAt = _now.AddMinutes(-_rng.Next(10, recent ? 7 * 24 * 60 : 540 * 24 * 60));

        var status = PickStatus(reportedAt);
        var customers = CustomersAffected();

        var outage = new Outage
        {
            Title = $"{cause} - {neighborhood}",
            Description = $"{cause} reported in {neighborhood}, {DisplayName(borough)}. Approx. {customers:N0} customers affected.",
            Borough = borough,
            Neighborhood = neighborhood,
            CustomersAffected = customers,
            Status = status,
            Priority = PriorityFor(customers),
            ReportedAt = reportedAt,
            ResolvedAt = status == OutageStatus.Resolved ? reportedAt.AddMinutes(_rng.Next(30, 48 * 60)) : null,
            CreatedAt = reportedAt,
        };
        outage.UpdatedAt = outage.ResolvedAt ?? reportedAt;

        // crews mostly work their home borough
        var crews = _rng.NextDouble() < 0.85 ? crewsByBorough[borough] : allCrews;
        string[] steps = ["Site assessment", repair, "Restore service and verify"];
        var stepCount = _rng.Next(1, steps.Length + 1);

        for (var i = 0; i < stepCount; i++)
            outage.WorkOrders.Add(CreateWorkOrder(outage, steps[i], Pick(crews)));

        return outage;
    }

    private WorkOrder CreateWorkOrder(Outage outage, string title, Crew crew)
    {
        var status = outage.Status switch
        {
            OutageStatus.Resolved => _rng.NextDouble() < 0.95 ? WorkOrderStatus.Completed : WorkOrderStatus.Cancelled,
            OutageStatus.Reported => _rng.NextDouble() < 0.5 ? WorkOrderStatus.Open : WorkOrderStatus.Assigned,
            _ => Pick([WorkOrderStatus.Assigned, WorkOrderStatus.InProgress, WorkOrderStatus.Completed]),
        };

        var createdAt = outage.ReportedAt.AddMinutes(_rng.Next(5, 60));

        return new WorkOrder
        {
            Title = title,
            Status = status,
            Priority = outage.Priority,
            // set FK not navigation - crew is detached after ChangeTracker.Clear()
            CrewId = status == WorkOrderStatus.Open ? null : crew.Id,
            CompletedAt = status == WorkOrderStatus.Completed ? outage.ResolvedAt ?? createdAt.AddHours(2) : null,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };
    }

    private OutageStatus PickStatus(DateTimeOffset reportedAt)
    {
        if (_now - reportedAt > TimeSpan.FromDays(7)) return OutageStatus.Resolved;

        return _rng.NextDouble() switch
        {
            < 0.25 => OutageStatus.Reported,
            < 0.50 => OutageStatus.Investigating,
            < 0.75 => OutageStatus.Restoring,
            _ => OutageStatus.Resolved,
        };
    }

    // mostly small outages, long tail of big ones (10 - ~10k customers)
    private int CustomersAffected() => (int)(10 * Math.Pow(1000, Math.Pow(_rng.NextDouble(), 2)));

    private Priority PriorityFor(int customers) => customers switch
    {
        >= 5000 => Priority.Critical,
        >= 1000 => Priority.High,
        >= 200 => Priority.Medium,
        _ => _rng.NextDouble() < 0.1 ? Priority.Medium : Priority.Low, // e.g. hospital on the block
    };

    private Borough PickBorough()
    {
        var roll = _rng.NextDouble();
        foreach (var (borough, weight) in BoroughWeights)
        {
            if (roll < weight) return borough;
            roll -= weight;
        }
        return Borough.Brooklyn;
    }

    private T Pick<T>(T[] items) => items[_rng.Next(items.Length)];

    private static string DisplayName(Borough b) => b == Borough.StatenIsland ? "Staten Island" : b.ToString();
}
