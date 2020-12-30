using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq.Expressions;

namespace EarlyXrm.QueryExpressionExtensions
{
    public class OrderExpression<T> where T : Entity
    {
        public static implicit operator OrderExpression(OrderExpression<T> self)
        {
            return new OrderExpression { AttributeName = self.AttributeName, OrderType = self.OrderType };
        }

        public OrderExpression(Expression<Func<T, object>> column, OrderType orderType = OrderType.Ascending)
        {
            AttributeName = column.LogicalName();
            OrderType = orderType;
        }

        public string AttributeName { get; set; }
        public OrderType OrderType { get; set; }
    }
}