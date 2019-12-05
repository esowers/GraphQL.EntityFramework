using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

static class QueryExecutor
{
    public static async Task<object> ExecuteQuery<TDbContext>(
        string query,
        ServiceCollection services,
        TDbContext dbContext,
        Inputs? inputs,
        Filters? filters)
        where TDbContext : DbContext
    {
        query = query.Replace("'", "\"");
        EfGraphQLConventions.RegisterInContainer(
            services,
            userContext => (TDbContext)((Dictionary<string, object>)userContext)[typeof(TDbContext).Name],
            dbContext.Model,
            userContext => filters);
        EfGraphQLConventions.RegisterConnectionTypesInContainer(services);
        await using var provider = services.BuildServiceProvider();
        using var schema = new Schema(provider);
        var documentExecuter = new EfDocumentExecuter();

        #region ExecutionOptionsWithFixIdTypeRule
        var executionOptions = new ExecutionOptions
        {
            Schema = schema,
            Query = query,
            Inputs = inputs,
            ValidationRules = FixIdTypeRule.CoreRulesWithIdFix
        };
        executionOptions.UserContext.Add(typeof(TDbContext).Name, dbContext);
        #endregion

        var executionResult = await documentExecuter.ExecuteWithErrorCheck(executionOptions);
        return executionResult.Data;
    }
}