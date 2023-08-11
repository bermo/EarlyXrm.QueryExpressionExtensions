using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace EarlyXrm.QueryExpressionExtensions;

public interface ILinkEntity<T> : ILinkEntity where T : Entity { }

public interface ILinkEntity
{
    ColumnSet Columns { get; }
    FilterExpression LinkCriteria { get; }
    string EntityAlias { get; set; }

    IEnumerable<OrderExpression> Orders { get; }
    IEnumerable<ConditionExpression> LinkConditions { get; }
    IEnumerable<LinkEntity> LinkEntities { get; }
    LambdaExpression ParentExpression { get; }
    //string ManyToManyName { get; set; }

    JoinOperator? JoinOperator { get; set; }

    IEnumerable<ILinkEntity> LinkQueryExpressions { get; }
}