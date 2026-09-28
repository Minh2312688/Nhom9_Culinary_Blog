using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.Features.Recipes.Commands;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

var connectionString = Environment.GetEnvironmentVariable("CULINARY_BLOG_POSTGRES_VERIFY");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Set CULINARY_BLOG_POSTGRES_VERIFY to the target database connection string.");
    return 2;
}

var builder = new NpgsqlConnectionStringBuilder(connectionString);
if (!string.Equals(builder.Database, "culinary_blog_auth", StringComparison.Ordinal))
{
    Console.Error.WriteLine("Refusing to run PostgreSQL behavior checks outside culinary_blog_auth.");
    return 2;
}

var options = new DbContextOptionsBuilder<AuthDbContext>().UseNpgsql(connectionString).Options;
var token = Guid.NewGuid();
var categoryId = Guid.NewGuid();
var recipeId = Guid.NewGuid();
var slug = $"pg-verification-{token:N}";

try
{
    await VerifyMigrationHistoryAsync(connectionString);
    await VerifyIdentityLoginAsync(connectionString);
    var authorId = await CreateProbeEntitiesAsync(options, categoryId, recipeId, slug);
    await VerifyRowVersionAndSoftDeleteAsync(options, categoryId);
    await VerifyStepMutationOrderAsync(options, recipeId, authorId);
    await VerifyRecipeSoftDeleteAsync(options, recipeId);
    Console.WriteLine("PostgreSQL checks passed: migration history, Identity password validation/role membership, stale RowVersion rejection, Category/Recipe soft delete, sequential step order, concurrent adds, and mixed add/delete.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"PostgreSQL behavior verification failed ({exception.GetType().Name}): {exception.Message}");
    return 1;
}
finally
{
    await CleanupAsync(connectionString, recipeId, categoryId);
}

static async Task VerifyMigrationHistoryAsync(string connectionString)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand("""
        SELECT COUNT(*) FROM "__EFMigrationsHistory"
        WHERE "MigrationId" IN ('20260921191147_Lab02PersonalInitialDatabase', '20260928050453_AlignAuthDbContextRecipeSchema')
        """, connection);
    if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 2)
        throw new InvalidOperationException("Target is missing expected AuthDbContext migration history.");
}

static async Task VerifyIdentityLoginAsync(string connectionString)
{
    var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:Postgres"] = connectionString,
        ["Redis:ConnectionString"] = "",
        ["Jwt:Key"] = $"Postgres-check-{Guid.NewGuid():N}-not-for-deployment"
    }).Build();
    var services = new ServiceCollection();
    services.AddSingleton<IConfiguration>(configuration);
    services.AddLogging();
    services.AddInfrastructure(configuration);
    await using var provider = services.BuildServiceProvider();
    await using var scope = provider.CreateAsyncScope();
    var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<CulinaryBlog.Infrastructure.Identity.ApplicationUser>>();
    var email = $"postgres-verification-{Guid.NewGuid():N}@example.invalid";
    var password = $"V{Guid.NewGuid():N}!7a";

    var registration = await identity.RegisterUserAsync(email, password, "PostgreSQL verification", "Author");
    if (!registration.Succeeded || string.IsNullOrWhiteSpace(registration.UserId))
        throw new InvalidOperationException("Could not register the temporary identity used for login verification.");

    try
    {
        var login = await identity.ValidateCredentialsAsync(email, password);
        if (!login.Succeeded || login.UserId != registration.UserId || login.Roles?.Contains("Author", StringComparer.Ordinal) != true)
            throw new InvalidOperationException("Identity login did not validate the password and Author role.");
    }
    finally
    {
        var temporaryUser = await userManager.FindByIdAsync(registration.UserId);
        if (temporaryUser is not null)
        {
            var deletion = await userManager.DeleteAsync(temporaryUser);
            if (!deletion.Succeeded)
                throw new InvalidOperationException("Could not remove the temporary login verification user.");
        }
    }
}

static async Task<string> CreateProbeEntitiesAsync(
    DbContextOptions<AuthDbContext> options, Guid categoryId, Guid recipeId, string slug)
{
    await using var db = new AuthDbContext(options);
    var user = await db.Users.OrderBy(x => x.Id).FirstOrDefaultAsync()
        ?? throw new InvalidOperationException("Target has no imported Identity user.");
    if (!await db.UserRoles.AnyAsync(x => x.UserId == user.Id) || !await db.Roles.AnyAsync())
        throw new InvalidOperationException("Target Identity user has no role membership.");
    var category = await db.Categories.OrderBy(x => x.Id).FirstOrDefaultAsync()
        ?? throw new InvalidOperationException("Target has no imported category.");

    db.Categories.Add(new Category { Id = categoryId, Name = "PostgreSQL verification", Slug = slug });
    db.Recipes.Add(new Recipe
    {
        Id = recipeId,
        Title = "PostgreSQL verification recipe",
        Slug = slug,
        CategoryId = category.Id,
        AuthorId = user.Id,
        Servings = 1,
        Difficulty = RecipeDifficulty.Easy,
        Status = RecipeStatus.Draft
    });
    await db.SaveChangesAsync();
    return user.Id;
}

static async Task VerifyRowVersionAndSoftDeleteAsync(DbContextOptions<AuthDbContext> options, Guid categoryId)
{
    await using var first = new AuthDbContext(options);
    await using var stale = new AuthDbContext(options);
    var firstCopy = await first.Categories.SingleAsync(x => x.Id == categoryId);
    var staleCopy = await stale.Categories.SingleAsync(x => x.Id == categoryId);

    firstCopy.Description = "first concurrent update";
    await first.SaveChangesAsync();
    staleCopy.Description = "stale concurrent update";
    try
    {
        await stale.SaveChangesAsync();
        throw new InvalidOperationException("A stale Category RowVersion update was accepted.");
    }
    catch (DbUpdateConcurrencyException)
    {
        // Expected: the first update must invalidate the stale version.
    }

    await using var deleting = new AuthDbContext(options);
    var category = await deleting.Categories.SingleAsync(x => x.Id == categoryId);
    deleting.Categories.Remove(category);
    await deleting.SaveChangesAsync();
    if (await deleting.Categories.AnyAsync(x => x.Id == categoryId))
        throw new InvalidOperationException("Soft-deleted Category remained visible through its query filter.");
    if (!await deleting.Categories.IgnoreQueryFilters().AnyAsync(x => x.Id == categoryId && x.IsDeleted))
        throw new InvalidOperationException("Category delete did not persist IsDeleted.");
}

static async Task VerifyStepMutationOrderAsync(DbContextOptions<AuthDbContext> options, Guid recipeId, string authorId)
{
    await using var db = new AuthDbContext(options);
    var currentUser = new VerificationUser(authorId);
    var cache = new NoOpRecipeCache();
    var mutationLock = new PostgresRecipeMutationLock(db);
    var add = new AddRecipeStepCommandHandler(db, currentUser, cache, mutationLock);
    var delete = new DeleteRecipeStepCommandHandler(db, currentUser, cache, mutationLock);

    var first = await add.Handle(new AddRecipeStepCommand(recipeId, "First", "First step", null, null), CancellationToken.None);
    var second = await add.Handle(new AddRecipeStepCommand(recipeId, "Second", "Second step", null, null), CancellationToken.None);
    if (first.StepNumber != 1 || second.StepNumber != 2)
        throw new InvalidOperationException("Added steps did not receive consecutive numbers.");

    await delete.Handle(new DeleteRecipeStepCommand(recipeId, first.Id), CancellationToken.None);
    var active = await db.RecipeSteps.Where(x => x.RecipeId == recipeId).OrderBy(x => x.StepNumber).ToListAsync();
    var deleted = await db.RecipeSteps.IgnoreQueryFilters().SingleAsync(x => x.Id == first.Id);
    if (active.Count != 1 || active[0].StepNumber != 1 || !deleted.IsDeleted)
        throw new InvalidOperationException("Deleting a step did not soft-delete and compact active numbering.");

    var third = await add.Handle(new AddRecipeStepCommand(recipeId, "Third", "Third step", null, null), CancellationToken.None);
    if (third.StepNumber != 2)
        throw new InvalidOperationException("Adding after a deletion did not append after the active steps.");

    var concurrentNumbers = await Task.WhenAll(
        AddStepConcurrentlyAsync(options, recipeId, authorId, "Concurrent A"),
        AddStepConcurrentlyAsync(options, recipeId, authorId, "Concurrent B"));
    if (!concurrentNumbers.Order().SequenceEqual([3, 4]))
        throw new InvalidOperationException("Concurrent step additions were not serialized into distinct consecutive numbers.");

    await using (var activeContext = new AuthDbContext(options))
    {
        var stepToDelete = await activeContext.RecipeSteps
            .Where(x => x.RecipeId == recipeId)
            .OrderBy(x => x.StepNumber)
            .Select(x => new { x.Id, x.StepNumber })
            .FirstAsync();
        var mixedResults = await Task.WhenAll(
            AddStepConcurrentlyAsync(options, recipeId, authorId, "Mixed concurrent add"),
            DeleteStepConcurrentlyAsync(options, recipeId, authorId, stepToDelete.Id));
        if (mixedResults[0] < 0)
            throw new InvalidOperationException("Concurrent add returned an invalid step number.");
    }

    await using var verify = new AuthDbContext(options);
    var finalOrder = await verify.RecipeSteps.Where(x => x.RecipeId == recipeId)
        .OrderBy(x => x.StepNumber).Select(x => x.StepNumber).ToArrayAsync();
    if (!finalOrder.SequenceEqual(Enumerable.Range(1, finalOrder.Length)))
        throw new InvalidOperationException("Concurrent add/delete left active steps with gaps or duplicate numbers.");
}

static async Task<int> AddStepConcurrentlyAsync(
    DbContextOptions<AuthDbContext> options, Guid recipeId, string authorId, string title)
{
    await using var db = new AuthDbContext(options);
    var handler = new AddRecipeStepCommandHandler(
        db, new VerificationUser(authorId), new NoOpRecipeCache(), new PostgresRecipeMutationLock(db));
    var result = await handler.Handle(
        new AddRecipeStepCommand(recipeId, title, "Concurrent verification step", null, null),
        CancellationToken.None);
    return result.StepNumber;
}

static async Task<int> DeleteStepConcurrentlyAsync(
    DbContextOptions<AuthDbContext> options, Guid recipeId, string authorId, Guid stepId)
{
    await using var db = new AuthDbContext(options);
    var handler = new DeleteRecipeStepCommandHandler(
        db, new VerificationUser(authorId), new NoOpRecipeCache(), new PostgresRecipeMutationLock(db));
    await handler.Handle(new DeleteRecipeStepCommand(recipeId, stepId), CancellationToken.None);
    return 0;
}

static async Task VerifyRecipeSoftDeleteAsync(DbContextOptions<AuthDbContext> options, Guid recipeId)
{
    await using var db = new AuthDbContext(options);
    var recipe = await db.Recipes.SingleAsync(x => x.Id == recipeId);
    db.Recipes.Remove(recipe);
    await db.SaveChangesAsync();
    if (await db.Recipes.AnyAsync(x => x.Id == recipeId))
        throw new InvalidOperationException("Soft-deleted Recipe remained visible through its query filter.");
    if (!await db.Recipes.IgnoreQueryFilters().AnyAsync(x => x.Id == recipeId && x.IsDeleted))
        throw new InvalidOperationException("Recipe delete did not persist IsDeleted.");
}

static async Task CleanupAsync(string connectionString, Guid recipeId, Guid categoryId)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using var transaction = await connection.BeginTransactionAsync();
    await using (var recipe = new NpgsqlCommand("DELETE FROM \"Recipes\" WHERE \"Id\" = @id", connection, transaction))
    {
        recipe.Parameters.AddWithValue("id", recipeId);
        await recipe.ExecuteNonQueryAsync();
    }
    await using (var category = new NpgsqlCommand("DELETE FROM \"Categories\" WHERE \"Id\" = @id", connection, transaction))
    {
        category.Parameters.AddWithValue("id", categoryId);
        await category.ExecuteNonQueryAsync();
    }
    await transaction.CommitAsync();
}

sealed record VerificationUser(string? UserId) : ICurrentUserService
{
    public bool IsAdmin => false;
    public bool IsAuthenticated => UserId is not null;
}

sealed class NoOpRecipeCache : IRecipeCache
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult(default(T));
    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
