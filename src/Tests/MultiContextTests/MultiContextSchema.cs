using GraphQL;
using Microsoft.Extensions.DependencyInjection;
using System;

public class MultiContextSchema :
    GraphQL.Types.Schema
{
    public MultiContextSchema(IServiceProvider provider) :
        base(provider)
    {
        Query = provider.GetRequiredService<MultiContextQuery>();
    }
}