using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Linq;
using System.Reflection;
using System;

namespace EarlyXrm.QueryExpressionExtensions
{

    public class ColumnSet<T> where T : Entity
    {
        public static implicit operator ColumnSet(ColumnSet<T> self)
        {
            if (self == null)
                return null;

            return new ColumnSet(self.Columns.ToArray());
        }

        public ColumnSet(params Expression<Func<T, object>>[] columns)
        {
            Columns = columns.Select(x => x.LogicalName());
        }

        public IEnumerable<string> Columns { get; private set; }
    }

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

    public class EntityCollection<T> : IEnumerable<T> 
        where T : Entity
    {
        public Collection<T> Entities { get; internal set; } = new Collection<T>();
        public bool MoreRecords { get; set; }
        public string PagingCookie { get; set; }
        public string MinActiveRowVersion { get; set; }
        public int TotalRecordCount { get; set; }
        public bool TotalRecordCountLimitExceeded { get; set; }
        public string EntityName { get; private set; }

        public static implicit operator EntityCollection(EntityCollection<T> self)
        {
            var val = new EntityCollection
            {
                EntityName = typeof(T).GetCustomAttribute<EntityLogicalNameAttribute>().LogicalName,
                MoreRecords = self.MoreRecords,
                PagingCookie = self.PagingCookie,
                MinActiveRowVersion = self.MinActiveRowVersion,
                TotalRecordCount = self.TotalRecordCount,
                TotalRecordCountLimitExceeded = self.TotalRecordCountLimitExceeded
            };

            val.Entities.AddRange(self.Entities);

            return val;
        }

        public EntityCollection() { }

        public EntityCollection(EntityCollection entityCollection, Dictionary<char, string> aliasMap = null)
        {
            EntityName = entityCollection.EntityName;
            MinActiveRowVersion = entityCollection.MinActiveRowVersion;
            MoreRecords = entityCollection.MoreRecords;
            PagingCookie = entityCollection.PagingCookie;
            TotalRecordCount = entityCollection.TotalRecordCount;
            TotalRecordCountLimitExceeded = entityCollection.TotalRecordCountLimitExceeded;

            var entities = entityCollection.Entities.Select(x => x.ToEntity<T>());

            if (aliasMap == null)
            {
                Entities = new Collection<T>(entities.ToList());
            }
            else
            {
                var grouping = entities.GroupBy(x => x.Id);
                var result = new Collection<T>();

                foreach (var children in grouping)
                {
                    var firstChild = children.First() as Entity;
                    foreach (var child in children)
                    {
                        RelateEntity(child, ref firstChild, aliasMap);
                    }

                    result.Add(firstChild as T);
                }

                foreach (var entity in result)
                    entity.Attributes.Where(x => x.Value as AliasedValue != null).ToList()
                        .ForEach(x => entity.Attributes.Remove(x.Key));

                Entities = result;
            }
        }

        protected virtual void RelateEntity(Entity child, ref Entity firstChild, Dictionary<char, string> aliasMap)
        {
            var aliased = child.Attributes.Where(x => x.Value as AliasedValue != null);
            var grouped = aliased
                            .Where(x => !x.Key.StartsWith("_"))
                            .Select(x => new KeyValuePair<string, object>($"{aliasMap[x.Key.First()]}{string.Join("", x.Key.Skip(1))}", x.Value))
                            .GroupBy(x => x.Key.Substring(0, x.Key.LastIndexOf('.')), x => (AliasedValue)x.Value)
                            .OrderBy(x => x.Key);

            var parents = new Dictionary<string, Entity> { { "", firstChild } };

            foreach (var group in grouped)
            {
                var entity = CreateEntityFromAlias(group);
                var names = group.Key.Split('.');
                var path = string.Join(".", names.Reverse().Skip(1).Reverse());
                var parent = parents[path];

                var split = names.Last().Split(':');
                var relationship = new Relationship(split[0]);
                if (split.Length > 1 && Enum.TryParse<EntityRole>(split[1], out var value))
                    relationship.PrimaryEntityRole = value;

                if (!parent.RelatedEntities.ContainsKey(relationship))
                {
                    parent.RelatedEntities.Add(relationship, new EntityCollection(new[] { entity }));
                    parents.Add(group.Key, entity);
                    continue;
                }

                var existing = parent.RelatedEntities[relationship].Entities.FirstOrDefault(x => x.Id == entity.Id);
                if (existing == null)
                {
                    parent.RelatedEntities[relationship].Entities.Add(entity);
                    parents.Add(group.Key, entity);
                    continue;
                }

                parents.Add(group.Key, existing);
            }
        }

        protected virtual Entity CreateEntityFromAlias(IGrouping<string, AliasedValue> group)
        {
            var logicalName = group.First().EntityLogicalName;
            var earlyType = typeof(T).Assembly.GetTypes().FirstOrDefault(x => x.GetCustomAttribute<EntityLogicalNameAttribute>()?.LogicalName == logicalName);
            var entity = Activator.CreateInstance(earlyType) as Entity;

            var atts = group.Where(x => x.EntityLogicalName == logicalName).Select(x => new KeyValuePair<string, object>(x.AttributeLogicalName, x.Value));
            entity.Attributes.AddRange(atts);

            if (entity.Id == Guid.Empty)
            {
                var idLogicalName = earlyType.GetProperty("Id").GetCustomAttribute<AttributeLogicalNameAttribute>().LogicalName;
                if (!entity.Attributes.ContainsKey(idLogicalName))
                    throw new ApplicationException("Missing Id!");
                entity.Id = (Guid)entity.Attributes[idLogicalName];
            }

            return entity;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return Entities.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Entities.GetEnumerator();
        }

        public void Add(T t)
        {
            Entities.Add(t);
        }
    }

    public class FilterExpression<T> where T : Entity
    {
        public static implicit operator FilterExpression(FilterExpression<T> self)
        {
            var val = new FilterExpression
            {
                FilterOperator = self.FilterOperator
            };

            foreach (var filter in self.Filters)
                val.Filters.Add(filter);

            foreach (var condition in self.Conditions)
                val.Conditions.Add(condition);

            return val;
        }

        public FilterExpression(LogicalOperator logicalOperator = LogicalOperator.And)
        {
            FilterOperator = logicalOperator;
        }

        public LogicalOperator FilterOperator { get; set; }

        public Collection<FilterExpression<T>> Filters { get; } = new Collection<FilterExpression<T>>();

        public Collection<ConditionExpression<T>> Conditions { get; set; } = new Collection<ConditionExpression<T>>();
    }

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

    public class LinkEntity<T, U> : LinkEntity<T>, ILinkEntity<T> 
        where T : Entity
        where U : Entity
    {
        public static implicit operator LinkEntity(LinkEntity<T, U> self)
        {
            var le = new LinkEntity<T> {
                lambdaExpression = self.lambdaExpression
            };

            le.EntityAlias = self.EntityAlias;
            le.JoinOperator = self.JoinOperator ?? default(JoinOperator);
            le.Columns = self.Columns ?? new ColumnSet(true);
            le.LinkCriteria = self.LinkCriteria;

            foreach (var order in self?.Orders ?? new Collection<OrderExpression<U>>())
                le.Orders.Add(order);

            foreach (var linkCondition in self?.LinkConditions ?? new Collection<ConditionExpression<U>>())
                le.LinkCriteria.Conditions.Add(linkCondition);

            foreach (var sublinkentity in self.LinkEntities)
                le.LinkEntities.Add(sublinkentity);

            return le;
        }

        public new Collection<OrderExpression<U>> Orders { get; set; } = new Collection<OrderExpression<U>>();

        public new ColumnSet<U> Columns { get; set; }

        public new FilterExpression<U> LinkCriteria { get; set; } = new FilterExpression<U>();

        public new Collection<ConditionExpression<U>> LinkConditions { get; set; } = new Collection<ConditionExpression<U>>();

        public new Collection<LinkEntity<U>> LinkEntities { get; set; } = new Collection<LinkEntity<U>>();

        LambdaExpression ILinkEntity.ParentExpression => lambdaExpression;

        IEnumerable<ILinkEntity> ILinkEntity.LinkQueryExpressions => LinkEntities.Select(x => x as ILinkEntity);

        ColumnSet ILinkEntity.Columns => Columns;

        FilterExpression ILinkEntity.LinkCriteria => LinkCriteria;
        IEnumerable<ConditionExpression> ILinkEntity.LinkConditions => LinkConditions.Select(x => (ConditionExpression)x);
        IEnumerable<OrderExpression> ILinkEntity.Orders => Orders.Select(x => (OrderExpression)x);
        IEnumerable<LinkEntity> ILinkEntity.LinkEntities => LinkEntities.Select(x => (LinkEntity)x);

        public LinkEntity(Expression<Func<T, U>> expression, JoinOperator? joinOperator = null)
        {
            lambdaExpression = expression;
            JoinOperator = joinOperator;
        }

        public LinkEntity(Expression<Func<T, IEnumerable<U>>> expression, JoinOperator? joinOperator = null)
        {
            lambdaExpression = expression;
            JoinOperator = joinOperator;
        }
    }

    public class LinkEntity<T> : ILinkEntity<T> 
        where T : Entity
    {
        public static implicit operator LinkEntity(LinkEntity<T> self)
        {
            var le = self.lambdaExpression;
            var me = le.Body as MemberExpression;
            var pi = me.Member as PropertyInfo;
            var relationship = pi.GetCustomAttribute<RelationshipSchemaNameAttribute>();
            var parentType = pi.DeclaringType;
            var parentLogical = parentType.GetCustomAttribute<EntityLogicalNameAttribute>().LogicalName;
            var primaryKey = parentType.GetProperty("Id")?.GetCustomAttribute<AttributeLogicalNameAttribute>()?.LogicalName;
            var linkproperty = parentType.GetProperties().Select(x => new { x, att = x.GetCustomAttribute<RelationshipSchemaNameAttribute>() })
                                .Where(x => x.att != null)
                                .FirstOrDefault(x => x.att.SchemaName == relationship.SchemaName && (relationship.PrimaryEntityRole == null || x.att.PrimaryEntityRole == relationship.PrimaryEntityRole)).x;
            var childType = linkproperty.PropertyType.IsGenericType ? linkproperty.PropertyType.GetGenericArguments()[0] : linkproperty.PropertyType;
            var childLogical = childType.GetCustomAttribute<EntityLogicalNameAttribute>().LogicalName;

            var linkschemaName = relationship.SchemaName;
            var parentProp = childType.GetProperties().Select(x => new { x, att = x.GetCustomAttribute<RelationshipSchemaNameAttribute>() })
                                .FirstOrDefault(y => y.att?.SchemaName == linkschemaName && (relationship.PrimaryEntityRole == null || y.att?.PrimaryEntityRole != relationship.PrimaryEntityRole)).x;
            var linkAttribute = parentProp.GetCustomAttribute<AttributeLogicalNameAttribute>()?.LogicalName;

            LinkEntity topLinkEntity;
            var linkEntity = new LinkEntity
            {
                EntityAlias = self.EntityAlias,
                JoinOperator = self.JoinOperator ?? default(JoinOperator),
                LinkCriteria = self.LinkCriteria,
            };

            if (linkAttribute != null) // one-to-many
            {
                linkEntity.LinkFromEntityName = parentLogical;
                linkEntity.LinkFromAttributeName = primaryKey;
                linkEntity.LinkToEntityName = childLogical;
                linkEntity.LinkToAttributeName = linkAttribute;
                topLinkEntity = linkEntity;
            }
            else
            {
                var childPk = childType.GetProperty("Id")?.GetCustomAttribute<AttributeLogicalNameAttribute>()?.LogicalName;
                linkAttribute = linkproperty.GetCustomAttribute<AttributeLogicalNameAttribute>()?.LogicalName;
                if (linkAttribute != null) // many-to-one
                {
                    linkEntity.LinkFromEntityName = parentLogical;
                    linkEntity.LinkFromAttributeName = linkAttribute;
                    linkEntity.LinkToEntityName = childLogical;
                    linkEntity.LinkToAttributeName = childPk;
                    topLinkEntity = linkEntity;
                }
                else // ManyToMany
                {
                    linkEntity.Columns = null;
                    linkEntity.LinkCriteria = null;
                    linkEntity.LinkFromEntityName = parentLogical;
                    linkEntity.LinkFromAttributeName = primaryKey;

                    var attributeProvider = pi.GetCustomAttribute<AttributeProviderAttribute>();
                    if (attributeProvider != null)
                    {
                        var type = Type.GetType(attributeProvider.TypeName);
                        linkschemaName = type.GetCustomAttribute<EntityLogicalNameAttribute>().LogicalName;
                    }
                    else // try to work out many-to-many name
                    {
                        if (linkschemaName.EndsWith("_association")) // Alot of built-in many-to-many junction entities have an "_association" suffix ...
                        {
                            linkschemaName = linkschemaName.Substring(0, linkschemaName.Length - "_association".Length);
                        }
                        else
                        {
                            var weirdSetup = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase)
                            {
                                { "knowledgearticle_category", "KnowledgeArticleCategory" },
                                { "ChannelAccessProfile_Privilege", "ChannelAccessProfileEntityAccessLevel" },
                                { "contact_subscription_association", "SubscriptionManuallyTrackedObject" },
                                { "serviceplan_appmodule", "ServicePlanAppModules" },
                                { "sample_product_knowledgebaserecord", "msdyn_sample_product_knowledgebaserecord" }
                            };

                            if (weirdSetup.ContainsKey(linkschemaName))
                            {
                                linkschemaName = weirdSetup[linkschemaName];
                            }
                        }
                    }

                    linkEntity.LinkToEntityName = linkschemaName.ToLower(); // use RelationshipSchema by convention for custom entities ...

                    linkEntity.LinkToAttributeName = primaryKey;

                    linkEntity.JoinOperator = Microsoft.Xrm.Sdk.Query.JoinOperator.LeftOuter;

                    topLinkEntity = new LinkEntity
                    {
                        EntityAlias = linkEntity.EntityAlias,
                        LinkFromEntityName = linkEntity.LinkToEntityName,
                        LinkFromAttributeName = childPk,
                        LinkToEntityName = childLogical,
                        LinkToAttributeName = childPk,
                    };

                    linkEntity.LinkEntities.Add(topLinkEntity);
                    linkEntity.EntityAlias = null;
                }
            }

            var iLinkEntity = self as ILinkEntity<T>;
            if (iLinkEntity != null)
            {
                topLinkEntity.Columns = iLinkEntity.Columns != null ? iLinkEntity.Columns : new ColumnSet(true);
                topLinkEntity.LinkCriteria = iLinkEntity.LinkCriteria;

                foreach (var linkCondition in iLinkEntity?.LinkConditions ?? new Collection<ConditionExpression>())
                    topLinkEntity.LinkCriteria.Conditions.Add(linkCondition);

                foreach (var order in iLinkEntity?.Orders ?? new Collection<OrderExpression>())
                    topLinkEntity.Orders.Add(order);

                foreach (var subLinkEntity in iLinkEntity.LinkEntities)
                    topLinkEntity.LinkEntities.Add(subLinkEntity);
            }

            return linkEntity;
        }

        internal LinkEntity() { }

        public LinkEntity(Expression<Func<T, Entity>> expression, JoinOperator? joinOperator = null)
        {
            lambdaExpression = expression;
            JoinOperator = joinOperator;
        }

        public LinkEntity(Expression<Func<T, IEnumerable<Entity>>> expression, JoinOperator? joinOperator = null)
        {
            lambdaExpression = expression;
            JoinOperator = joinOperator;
        }

        public Collection<OrderExpression> Orders { get; set; } = new Collection<OrderExpression>();

        public string EntityAlias { get; set; }

        public ColumnSet Columns { get; set; }

        internal LambdaExpression lambdaExpression;

        public Collection<LinkEntity> LinkEntities { get; set; } = new Collection<LinkEntity>();

        public FilterExpression LinkCriteria { get; set; } = new FilterExpression();

        public virtual IEnumerable<ConditionExpression> LinkConditions { get; set; } = new Collection<ConditionExpression>();

        LambdaExpression ILinkEntity.ParentExpression { get => lambdaExpression; }

        public JoinOperator? JoinOperator { get; set; }

        IEnumerable<ILinkEntity> ILinkEntity.LinkQueryExpressions => LinkEntities.Cast<ILinkEntity>();

        IEnumerable<OrderExpression> ILinkEntity.Orders => Orders;

        IEnumerable<LinkEntity> ILinkEntity.LinkEntities => LinkEntities;
    }

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

    public class QueryExpression<T> where T : Entity
    {
        public static implicit operator QueryExpression(QueryExpression<T> self)
        {
            var val = new QueryExpression
            {
                EntityName = typeof(T).GetCustomAttribute<EntityLogicalNameAttribute>().LogicalName,
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

        public ColumnSet<T> ColumnSet { get; set; }

        public Collection<ConditionExpression<T>> Conditions { get; set; } = new Collection<ConditionExpression<T>>();

        public FilterExpression<T> Criteria { get; set; } = new FilterExpression<T>();

        public JoinOperator DefaultJoinOperator { get; set; }
        public Collection<LinkEntity<T>> LinkEntities { get; set; } = new Collection<LinkEntity<T>>();

        public PagingInfo PageInfo { get; set; } = new PagingInfo();

        public Collection<OrderExpression<T>> Orders { get; set; } = new Collection<OrderExpression<T>>();

        public bool Distinct { get; set; }
        public int? TopCount { get; set; }
    }

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
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Linq;
using System.Reflection;
using System;

namespace EarlyXrm.QueryExpressionExtensions
{

    public class ColumnSet<T> where T : Entity
    {
        public static implicit operator ColumnSet(ColumnSet<T> self)
        {
            if (self == null)
                return null;

            return new ColumnSet(self.Columns.ToArray());
        }

        public ColumnSet(params Expression<Func<T, object>>[] columns)
        {
            Columns = columns.Select(x => x.LogicalName());
        }

        public IEnumerable<string> Columns { get; private set; }
    }

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

    public class EntityCollection<T> : IEnumerable<T> 
        where T : Entity
    {
        public Collection<T> Entities { get; internal set; } = new Collection<T>();
        public bool MoreRecords { get; set; }
        public string PagingCookie { get; set; }
        public string MinActiveRowVersion { get; set; }
        public int TotalRecordCount { get; set; }
        public bool TotalRecordCountLimitExceeded { get; set; }
        public string EntityName { get; private set; }

        public static implicit operator EntityCollection(EntityCollection<T> self)
        {
            var val = new EntityCollection
            {
                EntityName = typeof(T).GetCustomAttribute<EntityLogicalNameAttribute>().LogicalName,
                MoreRecords = self.MoreRecords,
                PagingCookie = self.PagingCookie,
                MinActiveRowVersion = self.MinActiveRowVersion,
                TotalRecordCount = self.TotalRecordCount,
                TotalRecordCountLimitExceeded = self.TotalRecordCountLimitExceeded
            };

            val.Entities.AddRange(self.Entities);

            return val;
        }

        public EntityCollection() { }

        public EntityCollection(EntityCollection entityCollection, Dictionary<char, string> aliasMap = null)
        {
            EntityName = entityCollection.EntityName;
            MinActiveRowVersion = entityCollection.MinActiveRowVersion;
            MoreRecords = entityCollection.MoreRecords;
            PagingCookie = entityCollection.PagingCookie;
            TotalRecordCount = entityCollection.TotalRecordCount;
            TotalRecordCountLimitExceeded = entityCollection.TotalRecordCountLimitExceeded;

            var entities = entityCollection.Entities.Select(x => x.ToEntity<T>());

            if (aliasMap == null)
            {
                Entities = new Collection<T>(entities.ToList());
            }
            else
            {
                var grouping = entities.GroupBy(x => x.Id);
                var result = new Collection<T>();

                foreach (var children in grouping)
                {
                    var firstChild = children.First() as Entity;
                    foreach (var child in children)
                    {
                        RelateEntity(child, ref firstChild, aliasMap);
                    }

                    result.Add(firstChild as T);
                }

                foreach (var entity in result)
                    entity.Attributes.Where(x => x.Value as AliasedValue != null).ToList()
                        .ForEach(x => entity.Attributes.Remove(x.Key));

                Entities = result;
            }
        }

        protected virtual void RelateEntity(Entity child, ref Entity firstChild, Dictionary<char, string> aliasMap)
        {
            var aliased = child.Attributes.Where(x => x.Value as AliasedValue != null);
            var grouped = aliased
                            .Where(x => !x.Key.StartsWith("_"))
                            .Select(x => new KeyValuePair<string, object>($"{aliasMap[x.Key.First()]}{string.Join("", x.Key.Skip(1))}", x.Value))
                            .GroupBy(x => x.Key.Substring(0, x.Key.LastIndexOf('.')), x => (AliasedValue)x.Value)
                            .OrderBy(x => x.Key);

            var parents = new Dictionary<string, Entity> { { "", firstChild } };

            foreach (var group in grouped)
            {
                var entity = CreateEntityFromAlias(group);
                var names = group.Key.Split('.');
                var path = string.Join(".", names.Reverse().Skip(1).Reverse());
                var parent = parents[path];

                var split = names.Last().Split(':');
                var relationship = new Relationship(split[0]);
                if (split.Length > 1 && Enum.TryParse<EntityRole>(split[1], out var value))
                    relationship.PrimaryEntityRole = value;

                if (!parent.RelatedEntities.ContainsKey(relationship))
                {
                    parent.RelatedEntities.Add(relationship, new EntityCollection(new[] { entity }));
                    parents.Add(group.Key, entity);
                    continue;
                }

                var existing = parent.RelatedEntities[relationship].Entities.FirstOrDefault(x => x.Id == entity.Id);
                if (existing == null)
                {
                    parent.RelatedEntities[relationship].Entities.Add(entity);
                    parents.Add(group.Key, entity);
                    continue;
                }

                parents.Add(group.Key, existing);
            }
        }

        protected virtual Entity CreateEntityFromAlias(IGrouping<string, AliasedValue> group)
        {
            var logicalName = group.First().EntityLogicalName;
            var earlyType = typeof(T).Assembly.GetTypes().FirstOrDefault(x => x.GetCustomAttribute<EntityLogicalNameAttribute>()?.LogicalName == logicalName);
            var entity = Activator.CreateInstance(earlyType) as Entity;

            var atts = group.Where(x => x.EntityLogicalName == logicalName).Select(x => new KeyValuePair<string, object>(x.AttributeLogicalName, x.Value));
            entity.Attributes.AddRange(atts);

            if (entity.Id == Guid.Empty)
            {
                var idLogicalName = earlyType.GetProperty("Id").GetCustomAttribute<AttributeLogicalNameAttribute>().LogicalName;
                if (!entity.Attributes.ContainsKey(idLogicalName))
                    throw new ApplicationException("Missing Id!");
                entity.Id = (Guid)entity.Attributes[idLogicalName];
            }

            return entity;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return Entities.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Entities.GetEnumerator();
        }

        public void Add(T t)
        {
            Entities.Add(t);
        }
    }

    public class FilterExpression<T> where T : Entity
    {
        public static implicit operator FilterExpression(FilterExpression<T> self)
        {
            var val = new FilterExpression
            {
                FilterOperator = self.FilterOperator
            };

            foreach (var filter in self.Filters)
                val.Filters.Add(filter);

            foreach (var condition in self.Conditions)
                val.Conditions.Add(condition);

            return val;
        }

        public FilterExpression(LogicalOperator logicalOperator = LogicalOperator.And)
        {
            FilterOperator = logicalOperator;
        }

        public LogicalOperator FilterOperator { get; set; }

        public Collection<FilterExpression<T>> Filters { get; } = new Collection<FilterExpression<T>>();

        public Collection<ConditionExpression<T>> Conditions { get; set; } = new Collection<ConditionExpression<T>>();
    }

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

    public class LinkEntity<T, U> : LinkEntity<T>, ILinkEntity<T> 
        where T : Entity
        where U : Entity
    {
        public static implicit operator LinkEntity(LinkEntity<T, U> self)
        {
            var le = new LinkEntity<T> {
                lambdaExpression = self.lambdaExpression
            };

            le.EntityAlias = self.EntityAlias;
            le.JoinOperator = self.JoinOperator ?? default(JoinOperator);
            le.Columns = self.Columns ?? new ColumnSet(true);
            le.LinkCriteria = self.LinkCriteria;

            foreach (var order in self?.Orders ?? new Collection<OrderExpression<U>>())
                le.Orders.Add(order);

            foreach (var linkCondition in self?.LinkConditions ?? new Collection<ConditionExpression<U>>())
                le.LinkCriteria.Conditions.Add(linkCondition);

            foreach (var sublinkentity in self.LinkEntities)
                le.LinkEntities.Add(sublinkentity);

            return le;
        }

        public new Collection<OrderExpression<U>> Orders { get; set; } = new Collection<OrderExpression<U>>();

        public new ColumnSet<U> Columns { get; set; }

        public new FilterExpression<U> LinkCriteria { get; set; } = new FilterExpression<U>();

        public new Collection<ConditionExpression<U>> LinkConditions { get; set; } = new Collection<ConditionExpression<U>>();

        public new Collection<LinkEntity<U>> LinkEntities { get; set; } = new Collection<LinkEntity<U>>();

        LambdaExpression ILinkEntity.ParentExpression => lambdaExpression;

        IEnumerable<ILinkEntity> ILinkEntity.LinkQueryExpressions => LinkEntities.Select(x => x as ILinkEntity);

        ColumnSet ILinkEntity.Columns => Columns;

        FilterExpression ILinkEntity.LinkCriteria => LinkCriteria;
        IEnumerable<ConditionExpression> ILinkEntity.LinkConditions => LinkConditions.Select(x => (ConditionExpression)x);
        IEnumerable<OrderExpression> ILinkEntity.Orders => Orders.Select(x => (OrderExpression)x);
        IEnumerable<LinkEntity> ILinkEntity.LinkEntities => LinkEntities.Select(x => (LinkEntity)x);

        public LinkEntity(Expression<Func<T, U>> expression, JoinOperator? joinOperator = null)
        {
            lambdaExpression = expression;
            JoinOperator = joinOperator;
        }

        public LinkEntity(Expression<Func<T, IEnumerable<U>>> expression, JoinOperator? joinOperator = null)
        {
            lambdaExpression = expression;
            JoinOperator = joinOperator;
        }
    }

    public class LinkEntity<T> : ILinkEntity<T> 
        where T : Entity
    {
        public static implicit operator LinkEntity(LinkEntity<T> self)
        {
            var le = self.lambdaExpression;
            var me = le.Body as MemberExpression;
            var pi = me.Member as PropertyInfo;
            var relationship = pi.GetCustomAttribute<RelationshipSchemaNameAttribute>();
            var parentType = pi.DeclaringType;
            var parentLogical = parentType.GetCustomAttribute<EntityLogicalNameAttribute>().LogicalName;
            var primaryKey = parentType.GetProperty("Id")?.GetCustomAttribute<AttributeLogicalNameAttribute>()?.LogicalName;
            var linkproperty = parentType.GetProperties().Select(x => new { x, att = x.GetCustomAttribute<RelationshipSchemaNameAttribute>() })
                                .Where(x => x.att != null)
                                .FirstOrDefault(x => x.att.SchemaName == relationship.SchemaName && (relationship.PrimaryEntityRole == null || x.att.PrimaryEntityRole == relationship.PrimaryEntityRole)).x;
            var childType = linkproperty.PropertyType.IsGenericType ? linkproperty.PropertyType.GetGenericArguments()[0] : linkproperty.PropertyType;
            var childLogical = childType.GetCustomAttribute<EntityLogicalNameAttribute>().LogicalName;

            var linkschemaName = relationship.SchemaName;
            var parentProp = childType.GetProperties().Select(x => new { x, att = x.GetCustomAttribute<RelationshipSchemaNameAttribute>() })
                                .FirstOrDefault(y => y.att?.SchemaName == linkschemaName && (relationship.PrimaryEntityRole == null || y.att?.PrimaryEntityRole != relationship.PrimaryEntityRole)).x;
            var linkAttribute = parentProp.GetCustomAttribute<AttributeLogicalNameAttribute>()?.LogicalName;

            LinkEntity topLinkEntity;
            var linkEntity = new LinkEntity
            {
                EntityAlias = self.EntityAlias,
                JoinOperator = self.JoinOperator ?? default(JoinOperator),
                LinkCriteria = self.LinkCriteria,
            };

            if (linkAttribute != null) // one-to-many
            {
                linkEntity.LinkFromEntityName = parentLogical;
                linkEntity.LinkFromAttributeName = primaryKey;
                linkEntity.LinkToEntityName = childLogical;
                linkEntity.LinkToAttributeName = linkAttribute;
                topLinkEntity = linkEntity;
            }
            else
            {
                var childPk = childType.GetProperty("Id")?.GetCustomAttribute<AttributeLogicalNameAttribute>()?.LogicalName;
                linkAttribute = linkproperty.GetCustomAttribute<AttributeLogicalNameAttribute>()?.LogicalName;
                if (linkAttribute != null) // many-to-one
                {
                    linkEntity.LinkFromEntityName = parentLogical;
                    linkEntity.LinkFromAttributeName = linkAttribute;
                    linkEntity.LinkToEntityName = childLogical;
                    linkEntity.LinkToAttributeName = childPk;
                    topLinkEntity = linkEntity;
                }
                else // ManyToMany
                {
                    linkEntity.Columns = null;
                    linkEntity.LinkCriteria = null;
                    linkEntity.LinkFromEntityName = parentLogical;
                    linkEntity.LinkFromAttributeName = primaryKey;

                    var attributeProvider = pi.GetCustomAttribute<AttributeProviderAttribute>();
                    if (attributeProvider != null)
                    {
                        var type = Type.GetType(attributeProvider.TypeName);
                        linkschemaName = type.GetCustomAttribute<EntityLogicalNameAttribute>().LogicalName;
                    }
                    else // try to work out many-to-many name
                    {
                        if (linkschemaName.EndsWith("_association")) // Alot of built-in many-to-many junction entities have an "_association" suffix ...
                        {
                            linkschemaName = linkschemaName.Substring(0, linkschemaName.Length - "_association".Length);
                        }
                        else
                        {
                            var weirdSetup = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase)
                            {
                                { "knowledgearticle_category", "KnowledgeArticleCategory" },
                                { "ChannelAccessProfile_Privilege", "ChannelAccessProfileEntityAccessLevel" },
                                { "contact_subscription_association", "SubscriptionManuallyTrackedObject" },
                                { "serviceplan_appmodule", "ServicePlanAppModules" },
                                { "sample_product_knowledgebaserecord", "msdyn_sample_product_knowledgebaserecord" }
                            };

                            if (weirdSetup.ContainsKey(linkschemaName))
                            {
                                linkschemaName = weirdSetup[linkschemaName];
                            }
                        }
                    }

                    linkEntity.LinkToEntityName = linkschemaName.ToLower(); // use RelationshipSchema by convention for custom entities ...

                    linkEntity.LinkToAttributeName = primaryKey;

                    linkEntity.JoinOperator = Microsoft.Xrm.Sdk.Query.JoinOperator.LeftOuter;

                    topLinkEntity = new LinkEntity
                    {
                        EntityAlias = linkEntity.EntityAlias,
                        LinkFromEntityName = linkEntity.LinkToEntityName,
                        LinkFromAttributeName = childPk,
                        LinkToEntityName = childLogical,
                        LinkToAttributeName = childPk,
                    };

                    linkEntity.LinkEntities.Add(topLinkEntity);
                    linkEntity.EntityAlias = null;
                }
            }

            var iLinkEntity = self as ILinkEntity<T>;
            if (iLinkEntity != null)
            {
                topLinkEntity.Columns = iLinkEntity.Columns != null ? iLinkEntity.Columns : new ColumnSet(true);
                topLinkEntity.LinkCriteria = iLinkEntity.LinkCriteria;

                foreach (var linkCondition in iLinkEntity?.LinkConditions ?? new Collection<ConditionExpression>())
                    topLinkEntity.LinkCriteria.Conditions.Add(linkCondition);

                foreach (var order in iLinkEntity?.Orders ?? new Collection<OrderExpression>())
                    topLinkEntity.Orders.Add(order);

                foreach (var subLinkEntity in iLinkEntity.LinkEntities)
                    topLinkEntity.LinkEntities.Add(subLinkEntity);
            }

            return linkEntity;
        }

        internal LinkEntity() { }

        public LinkEntity(Expression<Func<T, Entity>> expression, JoinOperator? joinOperator = null)
        {
            lambdaExpression = expression;
            JoinOperator = joinOperator;
        }

        public LinkEntity(Expression<Func<T, IEnumerable<Entity>>> expression, JoinOperator? joinOperator = null)
        {
            lambdaExpression = expression;
            JoinOperator = joinOperator;
        }

        public Collection<OrderExpression> Orders { get; set; } = new Collection<OrderExpression>();

        public string EntityAlias { get; set; }

        public ColumnSet Columns { get; set; }

        internal LambdaExpression lambdaExpression;

        public Collection<LinkEntity> LinkEntities { get; set; } = new Collection<LinkEntity>();

        public FilterExpression LinkCriteria { get; set; } = new FilterExpression();

        public virtual IEnumerable<ConditionExpression> LinkConditions { get; set; } = new Collection<ConditionExpression>();

        LambdaExpression ILinkEntity.ParentExpression { get => lambdaExpression; }

        public JoinOperator? JoinOperator { get; set; }

        IEnumerable<ILinkEntity> ILinkEntity.LinkQueryExpressions => LinkEntities.Cast<ILinkEntity>();

        IEnumerable<OrderExpression> ILinkEntity.Orders => Orders;

        IEnumerable<LinkEntity> ILinkEntity.LinkEntities => LinkEntities;
    }

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

    public class QueryExpression<T> where T : Entity
    {
        public static implicit operator QueryExpression(QueryExpression<T> self)
        {
            var val = new QueryExpression
            {
                EntityName = typeof(T).GetCustomAttribute<EntityLogicalNameAttribute>().LogicalName,
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

        public ColumnSet<T> ColumnSet { get; set; }

        public Collection<ConditionExpression<T>> Conditions { get; set; } = new Collection<ConditionExpression<T>>();

        public FilterExpression<T> Criteria { get; set; } = new FilterExpression<T>();

        public JoinOperator DefaultJoinOperator { get; set; }
        public Collection<LinkEntity<T>> LinkEntities { get; set; } = new Collection<LinkEntity<T>>();

        public PagingInfo PageInfo { get; set; } = new PagingInfo();

        public Collection<OrderExpression<T>> Orders { get; set; } = new Collection<OrderExpression<T>>();

        public bool Distinct { get; set; }
        public int? TopCount { get; set; }
    }

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
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Linq;
using System.Reflection;
using System;

namespace EarlyXrm.QueryExpressionExtensions
{

    public class ColumnSet<T> where T : Entity
    {
        public static implicit operator ColumnSet(ColumnSet<T> self)
        {
            if (self == null)
                return null;

            return new ColumnSet(self.Columns.ToArray());
        }

        public ColumnSet(params Expression<Func<T, object>>[] columns)
        {
            Columns = columns.Select(x => x.LogicalName());
        }

        public IEnumerable<string> Columns { get; private set; }
    }

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

    public class EntityCollection<T> : IEnumerable<T> 
        where T : Entity
    {
        public Collection<T> Entities { get; internal set; } = new Collection<T>();
        public bool MoreRecords { get; set; }
        public string PagingCookie { get; set; }
        public string MinActiveRowVersion { get; set; }
        public int TotalRecordCount { get; set; }
        public bool TotalRecordCountLimitExceeded { get; set; }
        public string EntityName { get; private set; }

        public static implicit operator EntityCollection(EntityCollection<T> self)
        {
            var val = new EntityCollection
            {
                EntityName = typeof(T).GetCustomAttribute<EntityLogicalNameAttribute>().LogicalName,
                MoreRecords = self.MoreRecords,
                PagingCookie = self.PagingCookie,
                MinActiveRowVersion = self.MinActiveRowVersion,
                TotalRecordCount = self.TotalRecordCount,
                TotalRecordCountLimitExceeded = self.TotalRecordCountLimitExceeded
            };

            val.Entities.AddRange(self.Entities);

            return val;
        }

        public EntityCollection() { }

        public EntityCollection(EntityCollection entityCollection, Dictionary<char, string> aliasMap = null)
        {
            EntityName = entityCollection.EntityName;
            MinActiveRowVersion = entityCollection.MinActiveRowVersion;
            MoreRecords = entityCollection.MoreRecords;
            PagingCookie = entityCollection.PagingCookie;
            TotalRecordCount = entityCollection.TotalRecordCount;
            TotalRecordCountLimitExceeded = entityCollection.TotalRecordCountLimitExceeded;

            var entities = entityCollection.Entities.Select(x => x.ToEntity<T>());

            if (aliasMap == null)
            {
                Entities = new Collection<T>(entities.ToList());
            }
            else
            {
                var grouping = entities.GroupBy(x => x.Id);
                var result = new Collection<T>();

                foreach (var children in grouping)
                {
                    var firstChild = children.First() as Entity;
                    foreach (var child in children)
                    {
                        RelateEntity(child, ref firstChild, aliasMap);
                    }

                    result.Add(firstChild as T);
                }

                foreach (var entity in result)
                    entity.Attributes.Where(x => x.Value as AliasedValue != null).ToList()
                        .ForEach(x => entity.Attributes.Remove(x.Key));

                Entities = result;
            }
        }

        protected virtual void RelateEntity(Entity child, ref Entity firstChild, Dictionary<char, string> aliasMap)
        {
            var aliased = child.Attributes.Where(x => x.Value as AliasedValue != null);
            var grouped = aliased
                            .Where(x => !x.Key.StartsWith("_"))
                            .Select(x => new KeyValuePair<string, object>($"{aliasMap[x.Key.First()]}{string.Join("", x.Key.Skip(1))}", x.Value))
                            .GroupBy(x => x.Key.Substring(0, x.Key.LastIndexOf('.')), x => (AliasedValue)x.Value)
                            .OrderBy(x => x.Key);

            var parents = new Dictionary<string, Entity> { { "", firstChild } };

            foreach (var group in grouped)
            {
                var entity = CreateEntityFromAlias(group);
                var names = group.Key.Split('.');
                var path = string.Join(".", names.Reverse().Skip(1).Reverse());
                var parent = parents[path];

                var split = names.Last().Split(':');
                var relationship = new Relationship(split[0]);
                if (split.Length > 1 && Enum.TryParse<EntityRole>(split[1], out var value))
                    relationship.PrimaryEntityRole = value;

                if (!parent.RelatedEntities.ContainsKey(relationship))
                {
                    parent.RelatedEntities.Add(relationship, new EntityCollection(new[] { entity }));
                    parents.Add(group.Key, entity);
                    continue;
                }

                var existing = parent.RelatedEntities[relationship].Entities.FirstOrDefault(x => x.Id == entity.Id);
                if (existing == null)
                {
                    parent.RelatedEntities[relationship].Entities.Add(entity);
                    parents.Add(group.Key, entity);
                    continue;
                }

                parents.Add(group.Key, existing);
            }
        }

        protected virtual Entity CreateEntityFromAlias(IGrouping<string, AliasedValue> group)
        {
            var logicalName = group.First().EntityLogicalName;
            var earlyType = typeof(T).Assembly.GetTypes().FirstOrDefault(x => x.GetCustomAttribute<EntityLogicalNameAttribute>()?.LogicalName == logicalName);
            var entity = Activator.CreateInstance(earlyType) as Entity;

            var atts = group.Where(x => x.EntityLogicalName == logicalName).Select(x => new KeyValuePair<string, object>(x.AttributeLogicalName, x.Value));
            entity.Attributes.AddRange(atts);

            if (entity.Id == Guid.Empty)
            {
                var idLogicalName = earlyType.GetProperty("Id").GetCustomAttribute<AttributeLogicalNameAttribute>().LogicalName;
                if (!entity.Attributes.ContainsKey(idLogicalName))
                    throw new ApplicationException("Missing Id!");
                entity.Id = (Guid)entity.Attributes[idLogicalName];
            }

            return entity;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return Entities.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Entities.GetEnumerator();
        }

        public void Add(T t)
        {
            Entities.Add(t);
        }
    }

    public class FilterExpression<T> where T : Entity
    {
        public static implicit operator FilterExpression(FilterExpression<T> self)
        {
            var val = new FilterExpression
            {
                FilterOperator = self.FilterOperator
            };

            foreach (var filter in self.Filters)
                val.Filters.Add(filter);

            foreach (var condition in self.Conditions)
                val.Conditions.Add(condition);

            return val;
        }

        public FilterExpression(LogicalOperator logicalOperator = LogicalOperator.And)
        {
            FilterOperator = logicalOperator;
        }

        public LogicalOperator FilterOperator { get; set; }

        public Collection<FilterExpression<T>> Filters { get; } = new Collection<FilterExpression<T>>();

        public Collection<ConditionExpression<T>> Conditions { get; set; } = new Collection<ConditionExpression<T>>();
    }

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

    public class LinkEntity<T, U> : LinkEntity<T>, ILinkEntity<T> 
        where T : Entity
        where U : Entity
    {
        public static implicit operator LinkEntity(LinkEntity<T, U> self)
        {
            var le = new LinkEntity<T> {
                lambdaExpression = self.lambdaExpression
            };

            le.EntityAlias = self.EntityAlias;
            le.JoinOperator = self.JoinOperator ?? default(JoinOperator);
            le.Columns = self.Columns ?? new ColumnSet(true);
            le.LinkCriteria = self.LinkCriteria;

            foreach (var order in self?.Orders ?? new Collection<OrderExpression<U>>())
                le.Orders.Add(order);

            foreach (var linkCondition in self?.LinkConditions ?? new Collection<ConditionExpression<U>>())
                le.LinkCriteria.Conditions.Add(linkCondition);

            foreach (var sublinkentity in self.LinkEntities)
                le.LinkEntities.Add(sublinkentity);

            return le;
        }

        public new Collection<OrderExpression<U>> Orders { get; set; } = new Collection<OrderExpression<U>>();

        public new ColumnSet<U> Columns { get; set; }

        public new FilterExpression<U> LinkCriteria { get; set; } = new FilterExpression<U>();

        public new Collection<ConditionExpression<U>> LinkConditions { get; set; } = new Collection<ConditionExpression<U>>();

        public new Collection<LinkEntity<U>> LinkEntities { get; set; } = new Collection<LinkEntity<U>>();

        LambdaExpression ILinkEntity.ParentExpression => lambdaExpression;

        IEnumerable<ILinkEntity> ILinkEntity.LinkQueryExpressions => LinkEntities.Select(x => x as ILinkEntity);

        ColumnSet ILinkEntity.Columns => Columns;

        FilterExpression ILinkEntity.LinkCriteria => LinkCriteria;
        IEnumerable<ConditionExpression> ILinkEntity.LinkConditions => LinkConditions.Select(x => (ConditionExpression)x);
        IEnumerable<OrderExpression> ILinkEntity.Orders => Orders.Select(x => (OrderExpression)x);
        IEnumerable<LinkEntity> ILinkEntity.LinkEntities => LinkEntities.Select(x => (LinkEntity)x);

        public LinkEntity(Expression<Func<T, U>> expression, JoinOperator? joinOperator = null)
        {
            lambdaExpression = expression;
            JoinOperator = joinOperator;
        }

        public LinkEntity(Expression<Func<T, IEnumerable<U>>> expression, JoinOperator? joinOperator = null)
        {
            lambdaExpression = expression;
            JoinOperator = joinOperator;
        }
    }

    public class LinkEntity<T> : ILinkEntity<T> 
        where T : Entity
    {
        public static implicit operator LinkEntity(LinkEntity<T> self)
        {
            var le = self.lambdaExpression;
            var me = le.Body as MemberExpression;
            var pi = me.Member as PropertyInfo;
            var relationship = pi.GetCustomAttribute<RelationshipSchemaNameAttribute>();
            var parentType = pi.DeclaringType;
            var parentLogical = parentType.GetCustomAttribute<EntityLogicalNameAttribute>().LogicalName;
            var primaryKey = parentType.GetProperty("Id")?.GetCustomAttribute<AttributeLogicalNameAttribute>()?.LogicalName;
            var linkproperty = parentType.GetProperties().Select(x => new { x, att = x.GetCustomAttribute<RelationshipSchemaNameAttribute>() })
                                .Where(x => x.att != null)
                                .FirstOrDefault(x => x.att.SchemaName == relationship.SchemaName && (relationship.PrimaryEntityRole == null || x.att.PrimaryEntityRole == relationship.PrimaryEntityRole)).x;
            var childType = linkproperty.PropertyType.IsGenericType ? linkproperty.PropertyType.GetGenericArguments()[0] : linkproperty.PropertyType;
            var childLogical = childType.GetCustomAttribute<EntityLogicalNameAttribute>().LogicalName;

            var linkschemaName = relationship.SchemaName;
            var parentProp = childType.GetProperties().Select(x => new { x, att = x.GetCustomAttribute<RelationshipSchemaNameAttribute>() })
                                .FirstOrDefault(y => y.att?.SchemaName == linkschemaName && (relationship.PrimaryEntityRole == null || y.att?.PrimaryEntityRole != relationship.PrimaryEntityRole)).x;
            var linkAttribute = parentProp.GetCustomAttribute<AttributeLogicalNameAttribute>()?.LogicalName;

            LinkEntity topLinkEntity;
            var linkEntity = new LinkEntity
            {
                EntityAlias = self.EntityAlias,
                JoinOperator = self.JoinOperator ?? default(JoinOperator),
                LinkCriteria = self.LinkCriteria,
            };

            if (linkAttribute != null) // one-to-many
            {
                linkEntity.LinkFromEntityName = parentLogical;
                linkEntity.LinkFromAttributeName = primaryKey;
                linkEntity.LinkToEntityName = childLogical;
                linkEntity.LinkToAttributeName = linkAttribute;
                topLinkEntity = linkEntity;
            }
            else
            {
                var childPk = childType.GetProperty("Id")?.GetCustomAttribute<AttributeLogicalNameAttribute>()?.LogicalName;
                linkAttribute = linkproperty.GetCustomAttribute<AttributeLogicalNameAttribute>()?.LogicalName;
                if (linkAttribute != null) // many-to-one
                {
                    linkEntity.LinkFromEntityName = parentLogical;
                    linkEntity.LinkFromAttributeName = linkAttribute;
                    linkEntity.LinkToEntityName = childLogical;
                    linkEntity.LinkToAttributeName = childPk;
                    topLinkEntity = linkEntity;
                }
                else // ManyToMany
                {
                    linkEntity.Columns = null;
                    linkEntity.LinkCriteria = null;
                    linkEntity.LinkFromEntityName = parentLogical;
                    linkEntity.LinkFromAttributeName = primaryKey;

                    var attributeProvider = pi.GetCustomAttribute<AttributeProviderAttribute>();
                    if (attributeProvider != null)
                    {
                        var type = Type.GetType(attributeProvider.TypeName);
                        linkschemaName = type.GetCustomAttribute<EntityLogicalNameAttribute>().LogicalName;
                    }
                    else // try to work out many-to-many name
                    {
                        if (linkschemaName.EndsWith("_association")) // Alot of built-in many-to-many junction entities have an "_association" suffix ...
                        {
                            linkschemaName = linkschemaName.Substring(0, linkschemaName.Length - "_association".Length);
                        }
                        else
                        {
                            var weirdSetup = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase)
                            {
                                { "knowledgearticle_category", "KnowledgeArticleCategory" },
                                { "ChannelAccessProfile_Privilege", "ChannelAccessProfileEntityAccessLevel" },
                                { "contact_subscription_association", "SubscriptionManuallyTrackedObject" },
                                { "serviceplan_appmodule", "ServicePlanAppModules" },
                                { "sample_product_knowledgebaserecord", "msdyn_sample_product_knowledgebaserecord" }
                            };

                            if (weirdSetup.ContainsKey(linkschemaName))
                            {
                                linkschemaName = weirdSetup[linkschemaName];
                            }
                        }
                    }

                    linkEntity.LinkToEntityName = linkschemaName.ToLower(); // use RelationshipSchema by convention for custom entities ...

                    linkEntity.LinkToAttributeName = primaryKey;

                    linkEntity.JoinOperator = Microsoft.Xrm.Sdk.Query.JoinOperator.LeftOuter;

                    topLinkEntity = new LinkEntity
                    {
                        EntityAlias = linkEntity.EntityAlias,
                        LinkFromEntityName = linkEntity.LinkToEntityName,
                        LinkFromAttributeName = childPk,
                        LinkToEntityName = childLogical,
                        LinkToAttributeName = childPk,
                    };

                    linkEntity.LinkEntities.Add(topLinkEntity);
                    linkEntity.EntityAlias = null;
                }
            }

            var iLinkEntity = self as ILinkEntity<T>;
            if (iLinkEntity != null)
            {
                topLinkEntity.Columns = iLinkEntity.Columns != null ? iLinkEntity.Columns : new ColumnSet(true);
                topLinkEntity.LinkCriteria = iLinkEntity.LinkCriteria;

                foreach (var linkCondition in iLinkEntity?.LinkConditions ?? new Collection<ConditionExpression>())
                    topLinkEntity.LinkCriteria.Conditions.Add(linkCondition);

                foreach (var order in iLinkEntity?.Orders ?? new Collection<OrderExpression>())
                    topLinkEntity.Orders.Add(order);

                foreach (var subLinkEntity in iLinkEntity.LinkEntities)
                    topLinkEntity.LinkEntities.Add(subLinkEntity);
            }

            return linkEntity;
        }

        internal LinkEntity() { }

        public LinkEntity(Expression<Func<T, Entity>> expression, JoinOperator? joinOperator = null)
        {
            lambdaExpression = expression;
            JoinOperator = joinOperator;
        }

        public LinkEntity(Expression<Func<T, IEnumerable<Entity>>> expression, JoinOperator? joinOperator = null)
        {
            lambdaExpression = expression;
            JoinOperator = joinOperator;
        }

        public Collection<OrderExpression> Orders { get; set; } = new Collection<OrderExpression>();

        public string EntityAlias { get; set; }

        public ColumnSet Columns { get; set; }

        internal LambdaExpression lambdaExpression;

        public Collection<LinkEntity> LinkEntities { get; set; } = new Collection<LinkEntity>();

        public FilterExpression LinkCriteria { get; set; } = new FilterExpression();

        public virtual IEnumerable<ConditionExpression> LinkConditions { get; set; } = new Collection<ConditionExpression>();

        LambdaExpression ILinkEntity.ParentExpression { get => lambdaExpression; }

        public JoinOperator? JoinOperator { get; set; }

        IEnumerable<ILinkEntity> ILinkEntity.LinkQueryExpressions => LinkEntities.Cast<ILinkEntity>();

        IEnumerable<OrderExpression> ILinkEntity.Orders => Orders;

        IEnumerable<LinkEntity> ILinkEntity.LinkEntities => LinkEntities;
    }

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

    public class QueryExpression<T> where T : Entity
    {
        public static implicit operator QueryExpression(QueryExpression<T> self)
        {
            var val = new QueryExpression
            {
                EntityName = typeof(T).GetCustomAttribute<EntityLogicalNameAttribute>().LogicalName,
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

        public ColumnSet<T> ColumnSet { get; set; }

        public Collection<ConditionExpression<T>> Conditions { get; set; } = new Collection<ConditionExpression<T>>();

        public FilterExpression<T> Criteria { get; set; } = new FilterExpression<T>();

        public JoinOperator DefaultJoinOperator { get; set; }
        public Collection<LinkEntity<T>> LinkEntities { get; set; } = new Collection<LinkEntity<T>>();

        public PagingInfo PageInfo { get; set; } = new PagingInfo();

        public Collection<OrderExpression<T>> Orders { get; set; } = new Collection<OrderExpression<T>>();

        public bool Distinct { get; set; }
        public int? TopCount { get; set; }
    }

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
