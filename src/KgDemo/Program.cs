using Neo4j.Driver;

// ---------------------------------------------------------------
// Connection settings (env vars first, local Docker defaults second)
// ---------------------------------------------------------------
var uri      = Environment.GetEnvironmentVariable("NEO4J_URI")      ?? "neo4j://localhost:7687";
var user     = Environment.GetEnvironmentVariable("NEO4J_USER")     ?? "neo4j";
var password = Environment.GetEnvironmentVariable("NEO4J_PASSWORD") ?? "password123";

// The driver is thread-safe and expensive to create: one per app, like a DbContext factory / connection pool.
await using var driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
// Neo4j can take ~20s to start in a fresh Codespace, so retry before giving up.
for (var attempt = 1; ; attempt++)
{
    try { await driver.VerifyConnectivityAsync(); break; }
    catch (Exception) when (attempt < 10)
    {
        Console.WriteLine($"Waiting for Neo4j... (attempt {attempt}/10)");
        await Task.Delay(TimeSpan.FromSeconds(3));
    }
}
Console.WriteLine($"Connected to {uri}\n");

await SeedAsync(driver);
await FindDevelopersAsync(driver, "Failing");
await TraceEvidenceAsync(driver, "Failing");

// ---------------------------------------------------------------
// 1. Seed: nodes + relationships (think INSERTs, but the "joins" are stored)
// ---------------------------------------------------------------
static async Task SeedAsync(IDriver driver)
{
    // Clean only our demo labels so reruns are idempotent
    await driver.ExecutableQuery(
        "MATCH (n) WHERE n:Developer OR n:File OR n:Service DETACH DELETE n")
        .ExecuteAsync();

    const string seed = """
        // Nodes
        CREATE (alice:Developer {name: 'Alice'})
        CREATE (bob:Developer   {name: 'Bob'})
        CREATE (carol:Developer {name: 'Carol'})

        CREATE (pay:File   {path: 'src/Payments/PaymentGateway.cs'})
        CREATE (order:File {path: 'src/Orders/OrderValidator.cs'})
        CREATE (mail:File  {path: 'src/Notify/EmailTemplates.cs'})

        CREATE (paySvc:Service    {name: 'PaymentService',      status: 'Failing'})
        CREATE (orderSvc:Service  {name: 'OrderService',        status: 'Healthy'})
        CREATE (notifySvc:Service {name: 'NotificationService', status: 'Healthy'})

        // Relationships carry their own properties (no join table needed)
        CREATE (alice)-[:COMMITTED {date: '2026-09-28', commit: 'a1f3c9'}]->(pay)
        CREATE (bob)  -[:COMMITTED {date: '2026-09-30', commit: 'b7e210'}]->(pay)
        CREATE (bob)  -[:COMMITTED {date: '2026-09-15', commit: 'c44d01'}]->(order)
        CREATE (carol)-[:COMMITTED {date: '2026-09-20', commit: 'd9a8b2'}]->(mail)

        CREATE (paySvc)   -[:DEPENDS_ON]->(pay)
        CREATE (paySvc)   -[:DEPENDS_ON]->(order)
        CREATE (orderSvc) -[:DEPENDS_ON]->(order)
        CREATE (notifySvc)-[:DEPENDS_ON]->(mail)
        """;

    var result = await driver.ExecutableQuery(seed).ExecuteAsync();
    var c = result.Summary.Counters;
    Console.WriteLine($"Seeded {c.NodesCreated} nodes and {c.RelationshipsCreated} relationships.\n");
}

// ---------------------------------------------------------------
// 2. The question: who touched code a failing service depends on?
//    (the SQL version needed 4 JOINs)
// ---------------------------------------------------------------
static async Task FindDevelopersAsync(IDriver driver, string status)
{
    const string query = """
        MATCH (d:Developer)-[:COMMITTED]->(:File)<-[:DEPENDS_ON]-(s:Service {status: $status})
        RETURN DISTINCT d.name AS developer
        ORDER BY developer
        """;

    // Always use parameters ($status), never string concatenation: same rule as SQL injection.
    var result = await driver.ExecutableQuery(query)
        .WithParameters(new { status })
        .ExecuteAsync();

    Console.WriteLine($"Developers linked to {status} services:");
    foreach (var record in result.Result)
        Console.WriteLine($"  - {record["developer"].As<string>()}");
    Console.WriteLine();
}

// ---------------------------------------------------------------
// 3. Traceability: return the evidence path, not just the answer.
//    This is what an agent would cite to explain its reasoning.
// ---------------------------------------------------------------
static async Task TraceEvidenceAsync(IDriver driver, string status)
{
    const string query = """
        MATCH (d:Developer)-[c:COMMITTED]->(f:File)<-[:DEPENDS_ON]-(s:Service {status: $status})
        RETURN d.name AS dev, c.commit AS commit, c.date AS date, f.path AS file, s.name AS service
        ORDER BY date DESC
        """;

    var result = await driver.ExecutableQuery(query)
        .WithParameters(new { status })
        .ExecuteAsync();

    Console.WriteLine("Evidence paths (most recent first):");
    foreach (var r in result.Result)
    {
        Console.WriteLine(
            $"  {r["dev"].As<string>()} -[COMMITTED {r["commit"].As<string>()} on {r["date"].As<string>()}]-> " +
            $"{r["file"].As<string>()} <-[DEPENDS_ON]- {r["service"].As<string>()}");
    }
}
