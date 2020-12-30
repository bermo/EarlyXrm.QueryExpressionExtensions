using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace EarlyXrm.QueryExpressionExtensions
{
    public static class QueryExpressionExtensions
    {
        public static EntityCollection<T> RetrieveMultiple<T>(this QueryExpression<T> queryExpression, IOrganizationService service) where T : Entity
        {
            var aliasMap = queryExpression.LinkEntities.RecursiveSetup(queryExpression.DefaultJoinOperator);

            var result = service.RetrieveMultiple(queryExpression);

            return new EntityCollection<T>(result, aliasMap);
        }

        public static T Retrieve<T>(this QueryExpression<T> queryExpression, IOrganizationService service, Guid id = default(Guid)) where T : Entity
        {
            if (id != default(Guid))
                queryExpression.Criteria.Conditions.Add(new ConditionExpression<T>(x => x.Id, id));

            var result = queryExpression.RetrieveMultiple(service);

            return result.Entities.FirstOrDefault();
        }

        public static EntityCollection<T> RetrieveMultiple<T>(this IOrganizationService service, QueryExpression<T> queryExpression) where T : Entity
        {
            if (queryExpression == null)
                queryExpression = new QueryExpression<T>();

            return queryExpression.RetrieveMultiple(service);
        }

        public static T Retrieve<T>(this IOrganizationService organizationService, Guid id, ColumnSet<T> columnSet = null) where T : Entity
        {
            var qe = new QueryExpression<T> { ColumnSet = columnSet };
            return Retrieve(qe, organizationService, id);
        }

        public static EntityCollection<T> RetrieveMultiple<T>(this QueryExpression<T> queryExpression, OrganizationServiceContext context) where T : Entity
        {
            if (queryExpression == null)
                queryExpression = new QueryExpression<T>();

            var aliasMap = queryExpression.LinkEntities.RecursiveSetup(queryExpression.DefaultJoinOperator);

            var result = context.Execute(new RetrieveMultipleRequest { Query = queryExpression }) as RetrieveMultipleResponse;

            return new EntityCollection<T>(result.EntityCollection, aliasMap);
        }

        private static Dictionary<char, string> RecursiveSetup(
            this IEnumerable<ILinkEntity> linkEntities,
            JoinOperator defaultJoinOperator,
            string parentAlias = null,
            Dictionary<char, string> aliasMap = null
        )
        {
            if (aliasMap == null)
                aliasMap = new Dictionary<char, string>();

            foreach(var linkEntity in linkEntities)
            {
                var me = linkEntity.ParentExpression;
                var pi = ((MemberExpression)me.Body).Member as PropertyInfo;
                var relationship = pi.GetCustomAttribute<RelationshipSchemaNameAttribute>();

                var aliasSeed = 'A';
                var last = aliasMap.Keys.Count == 0 ? --aliasSeed : aliasMap.Keys.Last();

                var prefix = parentAlias != null ? $"{parentAlias}." : "";
                var suffix = relationship.PrimaryEntityRole == null ? "" : $":{relationship.PrimaryEntityRole.Value}";
                var alias = $"{prefix}{relationship.SchemaName}{suffix}";
                aliasMap.Add(++last, alias);

                linkEntity.EntityAlias = last.ToString();

                if (linkEntity.JoinOperator == null)
                    linkEntity.JoinOperator = defaultJoinOperator;

                linkEntity.LinkQueryExpressions.RecursiveSetup(defaultJoinOperator, alias, aliasMap);
            }

            return aliasMap;
        }

        public static string LogicalName(this LambdaExpression lambda)
        {
            var expression = lambda.Body;
            if (expression.NodeType == ExpressionType.Convert)
                expression = ((UnaryExpression)expression).Operand;

            var me = expression as MemberExpression;

            var pi = me.Member as PropertyInfo;
            var customAttribute = pi.GetCustomAttribute<AttributeLogicalNameAttribute>();

            if (customAttribute == null) // usually the Id member from base Entity
                customAttribute = lambda.Type.GetGenericArguments().First().GetMember(pi.Name).First().GetCustomAttribute<AttributeLogicalNameAttribute>();

            return customAttribute?.LogicalName;
        }
    }
}