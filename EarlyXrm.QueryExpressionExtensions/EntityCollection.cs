using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace EarlyXrm.QueryExpressionExtensions;

public class EntityCollection<T> : IEnumerable<T> 
    where T : Entity
{
    public Collection<T> Entities { get; internal set; } = new Collection<T>();
    public bool MoreRecords { get; set; }
    public string PagingCookie { get; set; } = "";
    public string MinActiveRowVersion { get; set; } = "";
    public int TotalRecordCount { get; set; }
    public bool TotalRecordCountLimitExceeded { get; set; }
    public string EntityName { get; private set; } = "";

    public static implicit operator EntityCollection(EntityCollection<T> self)
    {
        var val = new EntityCollection
        {
            EntityName = typeof(T).GetCustomAttribute<EntityLogicalNameAttribute>()?.LogicalName,
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

    public EntityCollection(EntityCollection entityCollection, Dictionary<char, string>? aliasMap = null)
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

                result.Add((T)firstChild);
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
        var entity = Activator.CreateInstance(earlyType!) as Entity;

        var atts = group.Where(x => x.EntityLogicalName == logicalName).Select(x => new KeyValuePair<string, object>(x.AttributeLogicalName, x.Value));
        entity!.Attributes.AddRange(atts);

        if (entity.Id == Guid.Empty)
        {
            var idLogicalName = earlyType?.GetProperty("Id")?.GetCustomAttribute<AttributeLogicalNameAttribute>()?.LogicalName;
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