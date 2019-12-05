using GraphQL;
using Microsoft.Extensions.DependencyInjection;
using System;

public class DependencySchema :
    GraphQL.Types.Schema
{
    public DependencySchema(IServiceProvider provider) :
        base(provider)
    {
        Query = provider.GetService<DependencyQuery>();
    }
}