﻿using System.Collections.Generic;
using System.Threading.Tasks;
using EfLocalDb;
using GraphQL;
using GraphQL.EntityFramework;
using GraphQL.Utilities;
using Microsoft.Extensions.DependencyInjection;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

public class MultiContextTests :
    VerifyBase
{
    [Fact]
    public async Task Run()
    {
        GraphTypeTypeRegistry.Register<Entity1, Entity1Graph>();
        GraphTypeTypeRegistry.Register<Entity2, Entity2Graph>();

        var sqlInstance1 = new SqlInstance<DbContext1>(
            constructInstance: builder => new DbContext1(builder.Options));

        var sqlInstance2 = new SqlInstance<DbContext2>(
            constructInstance: builder => new DbContext2(builder.Options));

        var query = @"
{
  entity1
  {
    property
  },
  entity2
  {
    property
  }
}";

        var entity1 = new Entity1
        {
            Property = "the entity1"
        };
        var entity2 = new Entity2
        {
            Property = "the entity2"
        };

        var sqlDatabase1 = await sqlInstance1.Build();
        var sqlDatabase2 = await sqlInstance2.Build();
        await using (var dbContext = sqlDatabase1.NewDbContext())
        {
            dbContext.AddRange(entity1);
            await dbContext.SaveChangesAsync();
        }

        await using (var dbContext = sqlDatabase2.NewDbContext())
        {
            dbContext.AddRange(entity2);
            await dbContext.SaveChangesAsync();
        }

        await using var dbContext1 = sqlDatabase1.NewDbContext();
        await using var dbContext2 = sqlDatabase2.NewDbContext();
        var services = new ServiceCollection();

        services.AddSingleton<MultiContextQuery>();
        services.AddSingleton<Entity1Graph>();
        services.AddSingleton<Entity2Graph>();
        services.AddSingleton(dbContext1);
        services.AddSingleton(dbContext2);

        #region RegisterMultipleInContainer
        EfGraphQLConventions.RegisterInContainer(
            services,
            userContext => (DbContext1)((Dictionary<string, object>)userContext)["dbContext1"]);
        EfGraphQLConventions.RegisterInContainer(
            services,
            userContext => (DbContext2)((Dictionary<string, object>)userContext)["dbContext2"]);
        #endregion

        await using var provider = services.BuildServiceProvider();
        using var schema = new MultiContextSchema(provider);
        var documentExecuter = new EfDocumentExecuter();
        #region MultiExecutionOptions
        var executionOptions = new ExecutionOptions
        {
            Schema = schema,
            Query = query,
        };
        executionOptions.UserContext.Add("dbContext1", dbContext1);
        executionOptions.UserContext.Add("dbContext2", dbContext2);
        #endregion

        var executionResult = await documentExecuter.ExecuteWithErrorCheck(executionOptions);
        await Verify(executionResult.Data);
    }

    public MultiContextTests(ITestOutputHelper output) :
        base(output)
    {
    }
}
#region MultiUserContext
public class UserContext
{
    public UserContext(DbContext1 context1, DbContext2 context2)
    {
        DbContext1 = context1;
        DbContext2 = context2;
    }

    public readonly DbContext1 DbContext1;
    public readonly DbContext2 DbContext2;
}
#endregion