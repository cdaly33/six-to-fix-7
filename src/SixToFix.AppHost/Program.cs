// Prerequisites: PostgreSQL must be installed and running locally (no Docker required).
// Set the connection string via launchSettings.json, user secrets, or environment variable:
//   ConnectionStrings__sixtofix = "Host=localhost;Port=5432;Database=sixtofix;Username=postgres;Password=<your-password>"
var builder = DistributedApplication.CreateBuilder(args);

// Reference a locally-installed PostgreSQL — no container, no Docker dependency.
var db = builder.AddConnectionString("sixtofix");

var web = builder.AddProject<Projects.SixToFix_Web>("web")
    .WithReference(db);

builder.Build().Run();
