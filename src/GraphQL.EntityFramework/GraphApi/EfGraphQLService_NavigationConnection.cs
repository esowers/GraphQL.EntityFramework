using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GraphQL.Builders;
using GraphQL.Types;
using Microsoft.EntityFrameworkCore;

namespace GraphQL.EntityFramework
{
    partial class EfGraphQLService<TDbContext>
        where TDbContext : DbContext
    {
        public void AddNavigationConnectionField<TSource, TReturn>(
            ObjectGraphType<TSource> graph,
            string name,
            Func<ResolveEfFieldContext<TDbContext, TSource>, IEnumerable<TReturn>> resolve,
            Type? graphType = null,
            IEnumerable<QueryArgument>? arguments = null,
            IEnumerable<string>? includeNames = null,
            int pageSize = 10)
            where TReturn : class
        {
            Guard.AgainstNull(nameof(graph), graph);
            //build the connection field
            var connection = BuildListConnectionField(name, resolve, includeNames, pageSize, graphType);
            //add the field to the graph
            var field = graph.AddField(connection.FieldType);
            //append the optional where arguments to the field
            field.AddWhereArgument(arguments);
        }

        ConnectionBuilder<TSource> BuildListConnectionField<TSource, TReturn>(
            string name,
            Func<ResolveEfFieldContext<TDbContext, TSource>, IEnumerable<TReturn>> resolve,
            IEnumerable<string>? includeName,
            int pageSize,
            Type? graphType)
            where TReturn : class
        {
            Guard.AgainstNullWhiteSpace(nameof(name), name);
            Guard.AgainstNull(nameof(resolve), resolve);
            Guard.AgainstNegative(nameof(pageSize), pageSize);

            //lookup the graph type if not explicitly specified
            graphType ??= GraphTypeFinder.FindGraphType<TReturn>();
            //create a ConnectionBuilder<graphType, TSource> type by invoking the static Create<graphType> method on the generic type
            var builder = GetConnectionBuilder<TSource>(name, graphType);
            //set the page size
            builder.PageSize(pageSize);            
            //add the metadata for the tables to be included in the query to the ConnectionBuilder<graphType, TSource> object
            IncludeAppender.SetIncludeMetadata(builder.FieldType, name, includeName);
            //set the custom resolver
            builder.ResolveAsync(async context =>
            {
                var efFieldContext = BuildContext(context);
                //run the specified resolve function
                var enumerable = resolve(efFieldContext);
                //apply any query filters specified in the arguments
                enumerable = enumerable.ApplyGraphQlArguments(context);
                //apply the global filter on each individually enumerated item
                enumerable = await efFieldContext.Filters.ApplyFilter(enumerable, context.UserContext);
                //pagination does NOT occur server-side at this point, as the query has already executed
                var page = enumerable.ToList();
                //return the proper page of data
                return ConnectionConverter.ApplyConnectionContext(
                    page,
                    context.First,
                    context.After,
                    context.Last,
                    context.Before);
            });

            //return the field to be added to the graph
            return builder;
        }

        static ConnectionBuilder<TSource> GetConnectionBuilder<TSource>(string name, Type graphType)
        {
            var paramExp = System.Linq.Expressions.Expression.Parameter(typeof(string));
            var bodyExp = System.Linq.Expressions.Expression.Call(typeof(ConnectionBuilder<TSource>), "Create", new[] { graphType }, paramExp);
            var lambda = System.Linq.Expressions.Expression.Lambda<Func<string, ConnectionBuilder<TSource>>>(bodyExp, paramExp);
            var compiled = lambda.Compile();
            var x = compiled(name);
            return x;
        }
    }
}