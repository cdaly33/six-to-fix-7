// Prerequisites: Docker Desktop must be running before executing this AppHost.
// The PostgreSQL container depends on Docker, and this project relies on its launch profile for Aspire dashboard endpoints.
var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL — in dev, Aspire manages a container; in production, use an external connection string
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin(); // optional dev UI

var db = postgres.AddDatabase("sixtofix");

var web = builder.AddProject<Projects.SixToFix_Web>("web")
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();
