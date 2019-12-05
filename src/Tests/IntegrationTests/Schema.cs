using GraphQL;
using Microsoft.Extensions.DependencyInjection;
using System;

public class Schema :
    GraphQL.Types.Schema
{
    public Schema(IServiceProvider provider) :
        base(provider)
    {
        Query = provider.GetRequiredService<Query>();
    }
}