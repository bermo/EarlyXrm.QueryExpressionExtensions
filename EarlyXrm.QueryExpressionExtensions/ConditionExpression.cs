using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace EarlyXrm.QueryExpressionExtensions;

public partial class ConditionExpression<T> where T : Entity
{
    public static implicit operator ConditionExpression(ConditionExpression<T> self)
    {
        var val = new ConditionExpression
        {
            AttributeName = self.AttributeName,
            Operator = self.Operator
        };

        foreach(var value in self.Values)
        {
            var type = value.GetType();
            if (type == typeof(EntityReference))
                val.Values.Add(((EntityReference)value).Id);
            else if (type.IsEnum)
                val.Values.Add((int)value);
            else
                val.Values.Add(value);
        }

        return val;
    }

    public string AttributeName { get; set; }
    public ConditionOperator Operator { get; set; }
    public Collection<object> Values { get; } = new Collection<object>();

    public ConditionExpression() { }

    public ConditionExpression(Expression<Func<T, object>> column, object value)
    {
        AttributeName = column.LogicalName();
        Operator = ConditionOperator.Equal;
        Values.Add(value);
    }

    public ConditionExpression(Expression<Func<T, object>> column, ConditionOperator conditionOperator, params object[] values)
    {
        AttributeName = column.LogicalName();
        Operator = conditionOperator;
        foreach (var val in values)
            Values.Add(val);
    }

    public static ConditionExpression<T> Equal<U>(Expression<Func<T, U>> column, U value)
    {
        return new ConditionExpression<T>
        {
            AttributeName = column.LogicalName(),
            Operator = ConditionOperator.Equal,
            Values = { value }
        };
    }

    public static ConditionExpression<T> Null<U>(Expression<Func<T, U>> column)
    {
        return new ConditionExpression<T>
        {
            AttributeName = column.LogicalName(),
            Operator = ConditionOperator.Null
        };
    }

    public static ConditionExpression<T> NotNull<U>(Expression<Func<T, U>> column)
    {
        return new ConditionExpression<T>
        {
            AttributeName = column.LogicalName(),
            Operator = ConditionOperator.NotNull
        };
    }

    public static ConditionExpression<T> In<U>(Expression<Func<T, U>> column, params U[] values)
    {
        var condition = new ConditionExpression<T>
        {
            AttributeName = column.LogicalName(),
            Operator = ConditionOperator.In,
        };

        foreach (var value in values)
            condition.Values.Add(value);

        return condition;
    }
}