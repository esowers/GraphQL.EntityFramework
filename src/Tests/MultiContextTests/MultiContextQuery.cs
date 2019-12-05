using GraphQL.EntityFramework;
using GraphQL.Types;
using System.Collections.Generic;

public class MultiContextQuery :
    ObjectGraphType
{
    public MultiContextQuery(
        IEfGraphQLService<DbContext1> efGraphQlService1,
        IEfGraphQLService<DbContext2> efGraphQlService2)
    {
        efGraphQlService1.AddSingleField(
            graph: this,
            name: "entity1",
            resolve: context =>
            {
                var userContext = (DbContext1)((Dictionary<string, object>)context.UserContext)["dbContext1"];
                return userContext.Entities;
            });
        efGraphQlService2.AddSingleField(
            graph: this,
            name: "entity2",
            resolve: context =>
            {
                var userContext = (DbContext2)((Dictionary<string, object>)context.UserContext)["dbContext2"];
                return userContext.Entities;
            });
    }
}