﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EfLocalDb;
using GraphQL;
using GraphQL.EntityFramework;
using GraphQL.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

public class DependencyTests :
    VerifyBase
{
    static SqlInstance<DependencyDbContext> sqlInstance;

    static DependencyTests()
    {
        GraphTypeTypeRegistry.Register<Entity, EntityGraph>();

        sqlInstance = new SqlInstance<DependencyDbContext>(builder => new DependencyDbContext(builder.Options));
    }

    public DependencyTests(ITestOutputHelper output) :
        base(output)
    {
    }

    static string query = @"
{
  entities
  {
    property
  }
}";

    static class ModelBuilder
    {
        static ModelBuilder()
        {
            var builder = new DbContextOptionsBuilder<DependencyDbContext>();
            builder.UseSqlServer("Fake");
            using var context = new DependencyDbContext(builder.Options);
            Instance = context.Model;
        }

        public static readonly IModel Instance;
    }

    [Fact]
    public async Task ExplicitModel()
    {
        await using var database = await sqlInstance.Build();
        var dbContext = database.Context;
        await AddData(dbContext);
        var services = BuildServiceCollection();

        EfGraphQLConventions.RegisterInContainer(
            services,
            userContext => (DependencyDbContext)((Dictionary<string,object>)userContext)["dbContext"],
            model: ModelBuilder.Instance);
        await using var provider = services.BuildServiceProvider();
        using var schema = new DependencySchema(provider);
        var executionOptions = new ExecutionOptions
        {
            Schema = schema,
            Query = query,
            Inputs = null
        };
        executionOptions.UserContext.Add("dbContext", dbContext);

        await ExecutionResultData(executionOptions);
    }

    [Fact]
    public async Task ScopedDbContext()
    {
        await using var database = await sqlInstance.Build();
        var dbContext = database.Context;
        await AddData(dbContext);
        var services = BuildServiceCollection();
        services.AddScoped(x => database.Context);

        EfGraphQLConventions.RegisterInContainer(
            services,
            userContext => (DependencyDbContext)((Dictionary<string, object>)userContext)["dbContext"]);
        await using var provider = services.BuildServiceProvider();
        using var schema = new DependencySchema(provider);
        var executionOptions = new ExecutionOptions
        {
            Schema = schema,
            Query = query,
            Inputs = null
        };
        executionOptions.UserContext.Add("dbContext", dbContext);

        await ExecutionResultData(executionOptions);
    }

    [Fact]
    public async Task TransientDbContext()
    {
        await using var database = await sqlInstance.Build();
        var dbContext = database.Context;
        await AddData(dbContext);
        var services = BuildServiceCollection();
        services.AddTransient(x => database.Context);

        EfGraphQLConventions.RegisterInContainer(
            services,
            userContext => (DependencyDbContext)((Dictionary<string, object>)userContext)["dbContext"]);
        await using var provider = services.BuildServiceProvider();
        using var schema = new DependencySchema(provider);
        var executionOptions = new ExecutionOptions
        {
            Schema = schema,
            Query = query,
            Inputs = null
        };
        executionOptions.UserContext.Add("dbContext", dbContext);

        await ExecutionResultData(executionOptions);
    }

    [Fact]
    public async Task SingletonDbContext()
    {
        await using var database = await sqlInstance.Build();
        var dbContext = database.Context;
        await AddData(dbContext);
        var services = BuildServiceCollection();
        services.AddSingleton(database.Context);

        EfGraphQLConventions.RegisterInContainer(
            services,
            userContext => (DependencyDbContext)((Dictionary<string, object>)userContext)["dbContext"]);
        await using var provider = services.BuildServiceProvider();
        using var schema = new DependencySchema(provider);
        var executionOptions = new ExecutionOptions
        {
            Schema = schema,
            Query = query,
            Inputs = null
        };
        executionOptions.UserContext.Add("dbContext", dbContext);

        await ExecutionResultData(executionOptions);
    }

    static ServiceCollection BuildServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddSingleton<DependencyQuery>();
        services.AddSingleton(typeof(EntityGraph));
        return services;
    }

    static Task AddData(DependencyDbContext dbContext)
    {
        dbContext.AddRange(new Entity {Property = "A"});
        return dbContext.SaveChangesAsync();
    }

    static async Task ExecutionResultData(ExecutionOptions executionOptions)
    {
        var documentExecuter = new EfDocumentExecuter();
        var executionResult = await documentExecuter.ExecuteWithErrorCheck(executionOptions);
        var data = (Dictionary<string, object>) executionResult.Data;
        var objects = (List<object>) data.Single().Value;
        Assert.Single(objects);
    }
}