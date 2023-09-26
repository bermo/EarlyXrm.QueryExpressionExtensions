using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using System.Collections.ObjectModel;
using System.Reflection;

namespace EarlyXrm.QueryExpressionExtensions;

public class QueryExpression<T> where T : Entity
{
    public static implicit operator QueryExpression(QueryExpression<T> self)
    {
        var val = new QueryExpression
        {
            EntityName = typeof(T).GetCustomAttribute<EntityLogicalNameAttribute>()?.LogicalName,
            ColumnSet = self.ColumnSet ?? new ColumnSet(true),
            Distinct = self.Distinct,
            PageInfo = self.PageInfo,
            TopCount = self.TopCount,
            Criteria = self.Criteria
        };

        foreach (var order in self.Orders)
            val.Orders.Add(order);

        foreach (var condition in self.Conditions)
            val.Criteria.Conditions.Add(condition);

        foreach (var linkEntity in self.LinkEntities)
            val.LinkEntities.Add(linkEntity);

        return val;
    }

    public ColumnSet<T> ColumnSet { get; set; } = new (true);

    public Collection<ConditionExpression<T>> Conditions { get; set; } = new ();

    public FilterExpression<T> Criteria { get; set; } = new ();

    public JoinOperator DefaultJoinOperator { get; set; }
    public Collection<LinkEntity<T>> LinkEntities { get; set; } = new ();

    public PagingInfo PageInfo { get; set; } = new ();

    public Collection<OrderExpression<T>> Orders { get; set; } = new ();

    public bool Distinct { get; set; }
    public int? TopCount { get; set; }
}