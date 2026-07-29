# Contributing to Sentinela

## Development Environment Setup

### Prerequisites

| Tool               | Version   | Notes                                    |
|-------------------|-----------|------------------------------------------|
| Visual Studio 2022| 17.12+    | With .NET 9 and ASP.NET workloads        |
| .NET SDK          | 9.0+      | `dotnet --version` to verify             |
| Node.js           | 20 LTS+   | `node --version` to verify               |
| npm               | 10+        | `npm --version` to verify                |
| Docker Desktop    | 4.30+      | For infrastructure services              |
| Git               | 2.40+      |                                          |
| PowerShell 7+     | 7.4+       | For build scripts                        |

### Step 1: Clone Repository

```bash
git clone https://github.com/your-org/sentinela.git
cd sentinela
```

### Step 2: Start Infrastructure

```bash
docker compose up -d postgres redis rabbitmq seq
```

This starts the required services without the application containers (you'll run those from your IDE).

### Step 3: Configure Secrets

```bash
cd src/Services/Sentinela.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:PostgreSQL" "Host=localhost;Port=5432;Database=sentinela;Username=sentinela;Password=sentinela"
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379"
dotnet user-secrets set "ConnectionStrings:RabbitMQ" "amqp://sentinela:sentinela@localhost:5672"
dotnet user-secrets set "Jwt:Secret" "this-is-a-development-secret-key-that-must-be-256-bits"
```

Repeat for other services as needed.

### Step 4: Apply Migrations

```bash
cd src/Services/Sentinela.Api
dotnet ef database update
```

### Step 5: Seed Development Data

```bash
dotnet run --seed
```

### Step 6: Start the API

```bash
dotnet run
```

### Step 7: Start the Frontend

```bash
cd src/Web
npm install
npm run dev
```

The frontend should now be accessible at `https://localhost:3000` and the API at `https://localhost:5001`.

---

## Code Conventions

### General

- **Language**: English (code, comments, commit messages)
- **Encoding**: UTF-8
- **Line endings**: LF (Unix-style)
- **Trailing whitespace**: None
- **Final newline**: Yes (one blank line at end of file)

### C# Conventions

We follow [Microsoft C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) with the following additions:

```csharp
// File-scoped namespaces (no braces)
namespace Sentinela.Api.Controllers;

// Primary constructors for simple services
public class AlertService(ILogger<AlertService> logger, IAlertRepository repository) : IAlertService
{
    public async Task<AlertDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var alert = await repository.GetByIdAsync(id, ct);
        return alert?.ToDto() ?? throw new NotFoundException($"Alert {id} not found");
    }
}

// Implicit usings enabled globally
// Use `var` when type is obvious
// Use expression-bodied members where possible
```

**Naming:**
| Element               | Convention    | Example                     |
|----------------------|--------------|-----------------------------|
| Classes/Records      | PascalCase   | `AlertService`              |
| Interfaces           | IPascalCase  | `IAlertService`             |
| Methods              | PascalCase   | `GetByIdAsync()`            |
| Local variables      | camelCase    | `alertList`                 |
| Private fields       | _camelCase   | `_repository`               |
| Constants            | PascalCase   | `DefaultPageSize`           |
| Parameters           | camelCase    | `computerId`                |
| Enums                | PascalCase   | `AlertSeverity`             |

**Async:**
- All I/O operations should be async
- Suffix async methods with `Async`
- Always accept `CancellationToken` as last parameter
- Use `ValueTask` for hot-path methods that frequently complete synchronously

**Error Handling:**
- Use domain exceptions for business rule violations
- Use `Result<T>` pattern for expected failures (validation, not-found)
- Use exceptions for unexpected failures only
- Do not catch exceptions you cannot handle

### TypeScript / React Conventions

```typescript
// Use TypeScript strict mode
// Prefer interfaces over type aliases for objects
interface ComputerDto {
  id: string;
  name: string;
  status: ComputerStatus;
  lastHeartbeat: Date;
}

// Use functional components with hooks
const ComputerCard: React.FC<ComputerCardProps> = ({ computer, onSelect }) => {
  const { data: details } = useQuery({
    queryKey: ['computer', computer.id],
    queryFn: () => api.getComputer(computer.id)
  });

  return (
    <div onClick={() => onSelect(computer.id)}>
      <h3>{computer.name}</h3>
      <StatusBadge status={computer.status} />
    </div>
  );
};
```

**Naming:**
| Element           | Convention    | Example                     |
|------------------|--------------|-----------------------------|
| Components       | PascalCase   | `ComputerCard`              |
| Hooks            | use*         | `useComputerStatus`         |
| Functions        | camelCase    | `formatBytes()`             |
| Interfaces       | PascalCase   | `ComputerDto`               |
| Types            | PascalCase   | `ComputerStatus`            |
| Variables        | camelCase    | `computerList`              |
| Constants        | UPPER_SNAKECASE | `API_BASE_URL`           |
| Files            | kebab-case   | `computer-card.tsx`         |

**State Management:**
- Server state: TanStack Query (cache, refetch, optimistic updates)
- Client state: Zustand (UI state, modals, selections)
- URL state: React Router (filters, pagination)
- Form state: React Hook Form + Zod

### CSS / Styling

- Use Tailwind CSS utility classes for all styling
- Extract repeated patterns into React components (not custom CSS classes)
- Use `@apply` only for truly global styles (theme defaults)
- Follow Tailwind's responsive prefix convention (`md:`, `lg:`)

### SQL

- Use EF Core LINQ queries — no raw SQL unless performance requires it
- Name tables: plural (`Computers`, `Alerts`, `Users`)
- Name columns: PascalCase (`CreatedAt`, `ComputerId`)
- Use explicit foreign key properties
- Index foreign keys and frequently queried columns

---

## Testing Strategy

### Test Pyramid

```
        ╱╲
       ╱  ╲          E2E Tests (5%)
      ╱    ╲
     ╱──────╲
    ╱        ╲       Integration Tests (25%)
   ╱          ╲
  ╱──────────────╲
 ╱                ╲  Unit Tests (70%)
╱────────────────────╲
```

### Unit Tests

- Test domain logic, value objects, and application use cases
- Mock external dependencies (repositories, message bus)
- One assertion pattern per test (or closely related assertions)
- Naming: `{MethodUnderTest}_Should_{ExpectedBehavior}_When_{Condition}`

```csharp
[Fact]
public void Acknowledge_Should_SetStatusToAcknowledged_WhenAlertIsNew()
{
    // Arrange
    var alert = Alert.Create("Test Rule", AlertSeverity.High, Guid.NewGuid());
    var userId = Guid.NewGuid();

    // Act
    alert.Acknowledge(userId, "Investigating");

    // Assert
    alert.Status.Should().Be(AlertStatus.Acknowledged);
    alert.AcknowledgedBy.Should().Be(userId);
    alert.AcknowledgedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
}
```

### Integration Tests

- Test repository implementations, message bus integration, API controllers
- Use Testcontainers for PostgreSQL, Redis, RabbitMQ
- Use `WebApplicationFactory` for API integration tests
- Clean database state between test runs

```csharp
public class AlertsControllerTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly SentinelaDbContext _dbContext;

    public AlertsControllerTests(IntegrationTestWebAppFactory factory)
    {
        _client = factory.CreateClient();
        _dbContext = factory.DbContext;
    }

    [Fact]
    public async Task GetAlerts_ShouldReturnPaginatedResults()
    {
        // Arrange
        _dbContext.Alerts.Add(Alert.Create("Test", AlertSeverity.High, Guid.NewGuid()));
        await _dbContext.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync("/api/v1/alerts?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<PaginatedResult<AlertDto>>();
        content!.Data.Should().HaveCount(1);
    }
}
```

### Architecture Tests

Using NetArchTest, we enforce architectural boundaries:

```csharp
[Fact]
public void Infrastructure_Should_Not_Reference_Domain()
{
    var result = Types.InAssembly(typeof(SentinelaDbContext).Assembly)
        .That()
        .ResideInNamespace("Sentinela.Infrastructure")
        .Should()
        .NotHaveDependencyOn("Sentinela.Domain")
        .GetResult();

    result.IsSuccessful.Should().BeTrue();
}
```

### Test Tools

| Layer        | Framework           | Mocking         | Notes                     |
|-------------|--------------------|-----------------|---------------------------|
| Domain       | xUnit + FluentAssertions | N/A     | Pure logic, no mocks      |
| Application  | xUnit + FluentAssertions | NSubstitute |                          |
| API          | xUnit + Microsoft.AspNetCore.TestHost | NSubstitute | WebApplicationFactory |
| Integration  | xUnit + Testcontainers | N/A         | Real DB, Redis, RMQ      |
| Frontend     | Vitest + Testing Library | MSW (API mock) |                          |
| E2E          | Playwright          | N/A              | Full environment          |

### Running Tests

```bash
# All tests
dotnet test

# Specific project
dotnet test tests/Sentinela.Api.Tests

# Specific test
dotnet test --filter "GetAlerts_ShouldReturnPaginatedResults"

# Frontend tests
cd src/Web
npm test
npm run test:e2e
```

---

## Pull Request Process

### PR Checklist

Before submitting a PR:

- [ ] Code follows conventions (run `dotnet format` and `npm run lint`)
- [ ] All tests pass (`dotnet test` / `npm test`)
- [ ] New code has corresponding unit/integration tests
- [ ] API changes include OpenAPI updates and documentation
- [ ] Database migrations are included if schema changed
- [ ] No secrets or credentials in code
- [ ] Changes are rebased on latest `main`
- [ ] Commit messages follow conventional commits format

### Conventional Commits

```
<type>(<scope>): <description>

[optional body]

[optional footer]
```

**Types:**
| Type       | Usage                               |
|-----------|--------------------------------------|
| feat      | New feature                          |
| fix       | Bug fix                              |
| refactor  | Code change with no functional change|
| test      | Adding or updating tests             |
| docs      | Documentation changes                |
| perf      | Performance improvement              |
| chore     | Build, CI, dependencies              |
| style     | Formatting, whitespace               |

**Examples:**
```
feat(alerts): add bulk acknowledge endpoint
fix(agent): handle null process user name during collection
refactor(api): extract alert validation to pipeline behavior
docs(api): add rate limiting documentation
test(correlation): add unit tests for pattern detection
```

### Branch Strategy

```
main           ← Production-ready code
  └── develop  ← Integration branch
       ├── feature/alert-bulk-acknowledge
       ├── fix/agent-null-ref
       └── refactor/api-validation
```

- Branch from `develop`
- Name: `{type}/{short-description}` (e.g., `feat/alert-rules`, `fix/agent-memory-leak`)
- PR target: `develop`
- Release branches merge `develop` → `main`

### Review Process

1. **Author** creates PR with description and screenshots (for UI changes)
2. **CI** runs linting, tests, and builds automatically
3. **Reviewer 1** checks code quality, tests, and conventions
4. **Reviewer 2** (for significant changes) checks architecture and security
5. **Author** addresses feedback, updates PR
6. **Approval** from at least one reviewer
7. **Merge** (squash merge into develop)

---

## Coding Standards

### Required Analyzers

The project uses:
- **.NET Analyzers** (built-in)
- **SonarAnalyzer.CSharp** for additional code quality checks
- **StyleCop.Analyzers** for style consistency
- **Roslynator** for best practices

Run analysis:

```bash
dotnet build /p:RunAnalyzersDuringBuild=true
```

### Documentation

- XML documentation comments on all public APIs
- README for each service explaining purpose and configuration
- Swagger XML docs generated automatically
- Architecture Decision Records (ADRs) for significant decisions

### Performance Guidelines

- Use `AsNoTracking()` for read-only queries
- Batch database operations when processing multiple entities
- Use streaming for large result sets
- Avoid `IEnumerable<T>` for materialized collections — use arrays or lists
- Use `StringBuilder` for string concatenation in loops
- Pool HTTP connections with `IHttpClientFactory`
- Use `ValueTask` for frequently synchronous hot paths

### Security Guidelines

- Never log passwords, tokens, or secrets
- Validate all input (FluentValidation on every command)
- Use parameterized queries (never raw string concatenation)
- Sanitize any user input displayed in UI
- Use `[Authorize]` attribute on all controllers by default
- Apply principle of least privilege for service accounts
- Use HTTPS in all environments
