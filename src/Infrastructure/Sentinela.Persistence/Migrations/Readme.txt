=== Migrations Commands ===

Add a new migration:
  dotnet ef migrations add MigrationName --project src/Infrastructure/Sentinela.Persistence --startup-project src/Web/Sentinela.Api

Remove last migration (before committing):
  dotnet ef migrations remove --project src/Infrastructure/Sentinela.Persistence --startup-project src/Web/Sentinela.Api

Apply migrations to database:
  dotnet ef database update --project src/Infrastructure/Sentinela.Persistence --startup-project src/Web/Sentinela.Api

Generate SQL script:
  dotnet ef migrations script --project src/Infrastructure/Sentinela.Persistence --startup-project src/Web/Sentinela.Api --output migrations.sql

List pending migrations:
  dotnet ef migrations list --project src/Infrastructure/Sentinela.Persistence --startup-project src/Web/Sentinela.Api

Note: Ensure the startup project has a reference to Microsoft.EntityFrameworkCore.Design
