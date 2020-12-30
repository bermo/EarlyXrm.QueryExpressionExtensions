# QueryExpressionExtensions
A Dynamics library that provides a generic implementation of the QueryExpression class (and similar associated classes) for use with early-bound entities.
The library also provides extension some methods that automatically re-hydrates a returned datamodel up to 10 levels deep.

# Type Safety
standard
...
new QueryExpression("ee_test")
{
    ColumnSet = new ColumnSet("ee_name"),
    Criteria =
    {
        Conditions =
        {
            new ConditionExpression("ee_name", ConditionOperator.Equal, "test")
        }
    },
    Orders =
    {
        new OrderExpression("ee_name", OrderType.Ascending)
    }
};
...

generic version
...
new QueryExpression<Test>
{
    ColumnSet = new ColumnSet<Test>(x => x.Name),
    Criteria =
    {
        Conditions =
        {
            new ConditionExpression<Test>(x => x.Name, ConditionOperator.Equal, "test")
        }
    },
    Orders =
    {
        new OrderExpression<Test>(x => x.Name, OrderType.Ascending)
    }
};
...