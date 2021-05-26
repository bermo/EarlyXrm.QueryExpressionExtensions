# QueryExpressionExtensions

[![Coverage Status](https://coveralls.io/repos/github/bermo/EarlyXrm.QueryExpressionExtensions/badge.svg?branch=master)](https://coveralls.io/github/bermo/EarlyXrm.QueryExpressionExtensions?branch=master)

A Dynamics library that provides generic implementations of many `QueryExpression` related classes for use with early-bound entities.
The library also provides extension methods that re-hydrates an aliased flattened entity model.

## Basic type safety

The `IOrganizationService` interface provides a `Retrieve` helper method to get a single entity. 
The `columnSet` parameter of this method relies on strings and is therefore not very safe:
```
Entity unsafeAccount = service.Retrieve("account", id, new ColumnSet("name"));
```

Use the generic ColumnSet class which makes the columnsSet parameter safe at least:
```
Entity somewhatSafeAccount = service.Retrieve("account", id, new ColumnSet<Account>(x => x.Name));
```

There is also a generic version of the Retrieve function which also casts the result:
```
Account safeAccount = service.Retrieve<Account>(id, new ColumnSet<Account>(x => x.Name));
```

## QueryExpression type safety

When retrieving more than a single entity, a `QueryExpression` can be passed to `RetrieveMuliple` which both lack type safety:

```
var accountsQuery = new QueryExpression("account"){ ColumnSet = new ColumnSet("name") };
DataCollection<Entity> entities = service.RetrieveMultiple(accountsQuery).Entities;
```

Here are some other `QueryExpression` properties that similary lack type safety:
```
var unsafeQuery = new QueryExpression("account"){
    ColumnSet = new ColumnSet("name"),
    Criteria = {
        Conditions = {
            new ConditionExpression("name", ConditionOperator.Equal, "unsafe")
        }
    },
    Orders = {
        new OrderExpression("name", OrderType.Ascending)
    }
};
``` 

Likewise, use the other generic classes provided by `QueryExpressionExtensions` to improve type safety:
```
var somewhatSafeQuery = new QueryExpression("account"){
    ColumnSet = new ColumnSet<Account>(x => x.Name),
    Criteria = {
        Conditions = {
            new ConditionExpression<Account>(x => x.Name, ConditionOperator.Equal, "somewhatSafe")
        }
    },
    Orders = {
        new OrderExpression<Account>(x => x.Name, OrderType.Ascending)
    }
};
``` 

A generic version of `QueryExpression` itself can be be used - which also includes some extra helpers:
```
var moreSafeQuery = new QueryExpression<Account>{
    ColumnSet = new ColumnSet<Account>(x => x.Name),
    // Criteria = {}, // OR conditions (and therefore Criteria) are not necessary 90% of the time ...
    Conditions = { 
        new ConditionExpression<Account>(x => x.Name, ConditionOperator.Equal, "moreSafe")
    },
    Orders = {
        new OrderExpression<Account>(x => x.Name) // generic version defaults to Ascending
    }
};
``` 

And finally, some static versions of `ConditionExpression` that enforce parameter type matching too:
```
var safeQuery = new QueryExpression<Account>{
    ColumnSet = new ColumnSet<Account>(x => x.Name),
    Conditions = {
        ConditionExpression<Account>.Equal(x => x.Name, "safe") // in this case only accepts a string
    },
    Orders = {
        new OrderExpression<Account>(x => x.Name)
    }
};
```

## RetrieveMultiple extensions

As previously mentioned, the `IOrganizationService` interface provides the `RetrieveMultiple` helper method for direct access to an `EntityCollection`.
Generic versions of these methods have been provided that return strongly-typed entities:

```
var accountsQuery = new QueryExpression<Account> {...};
```
```
IEnumerable<Account> accounts = service.RetrieveMultiple<Account>(accountsQuery).Entities;
```

Unfortunately if the generic type is not provided on the `RetieveMultiple` call, the query is interrupted as a non-generic `QueryExpression`, and the auto strong-typing is lost:

```
DataCollection<Entity> entities = service.RetrieveMultiple(accountsQuery).Entities;
```

Therefore, an extension method has also been provided that works the other way so the generic type doesn't have to be provided twice:

```
IEnumerable<Account> accounts = accountsQuery.RetrieveMultiple(service).Entities; // accountQuery and service are reversed
```

## Retrieving a complex entity model

Using the `IOrganizationService`, it seems the only (built-in) way to retrieve a complex entity with hydrated related children is via a `RetrieveRequest`.  
Unfortunately `RetrieveRequest` has some serious limitations as it can only deal with relationships one-level above the root, and only works for a single entity at a time!

```
var accountRef = new EntityReference("account", id);
RetrieveResponse result = service.Execute(new RetrieveRequest { 
    Target = accountRef,
    RelatedEntitiesQuery = {
        new Relationship ("contact_customer_accounts", new QueryExpression("contact"){ ColumnSet = new ColumnSet(true) }
    }  
};
Account hydratedAccount = result.Entity.ToEntity<Account>();
IEnumerable<Contact> contacts = hydratedAccount.Contacts; 
```
Due to these limitations, this turns out to be useful in only a limited set of circumstances, and the code is quite cumbersome as well.

## Re-hydrating a flattened EntityCollection

When querying entities via `IOrganizationService`'s `RetrieveMulipleRequest` (or the `RetrieveMultiple` shortcut), the Dynamics API allows SQL like joins to be performed by adding `LinkEntities` to the QueryExpression:

```
var accountsQuery = new QueryExpression("account") {
    LinkEntities = {
        new LinkEntity {
            LinkFromAttributeName = "accountid",
            LinkFromEntityName = "account",
            LinkToAttributeName = "parentcustomerid",
            LinkToEntityName = "contact"
            Columns = new ColumnSet(true)
        }
    }
};
```

The entity data returned as part of one of these queries uses a flattened `AliasedAttribute` syntax to represent an entity heirachies:



Both setting up a valid `LinkEntity` query, and re-hydrating the flattened result set can be a very time consuming and error prone process!

Fortunately when helper generic `LinkEntity` methods are used along with generic versions of `QueryExpression` and `RetrieveMultiple`, all the heavy lifting is done for you:

```
var accountJoinQuery = new QueryExpression<Account> {
    LinkEntities = {
        new LinkEntity<Account>(x => x.contact_customer_accounts)
    }
};
IEnumerable<Account> accounts = accountJoinQuery.RetrieveMultiple(accountQuery).Entities;
IEnumerable<Contact> firstAccountContacts = accounts.First().Contacts;
```

This also works for a single entity when the `Retrieve` extension method is used off a generic `QueryExpression`:

```
Account account = new QueryExpression<Account> {
    LinkEntities = {
        new LinkEntity<Account>(x => x.contact_customer_accounts)
    }
}.Retrieve(id);
IEnumerable<Contact> contacts = account.Contacts; 
```

More complex sub-queries can be built using another generic `LinkEntity` constructor that takes two generic types:
```
var accountComplexQuery = new QueryExpression<Account> {
    LinkEntities = {
        new LinkEntity<Account>(x => x.lk_contactbase_createdby),
        new LinkEntity<Account, Contact>(x => x.contact_customer_accounts){
            LinkEntities = {
                new LinkEntity<Contact>(x => x.Contact_Emails)
            }
        }
    }
};
```

Note, Dynamics has a hard limit to the number of `LinkEntity` calls allowed as part of a single `QueryExpression` to 10.

## Putting it all together

It is possible to use all of the preceeding concepts together to greatly improve your Dynamics API type safety and overall performance (in appropriate scenarios):

```
var acountsComplex = new QueryExpression<Account> {
    ColumnSet = new ColumnSet<Account>(x => x.AccountName, x => x.EmailAddress),
    Conditions = {
        ConditionExpression<Account>.In(x => x.Industry, Account_Industry.Accounting, Account_Industry.Consulting)
    },
    Order = {
        new OrderExpression<Account>(x => x.CreatedOn)
    },
    LinkEntities = {
        new LinkEntity<Account>(x => x.lk_contactbase_createdby){
            Columns = new ColumnSet(x => x.FullName)
        }
        new LinkEntity<Account, Contact>(x => x.contact_customer_accounts){
            Columns = new ColumnSet<Contact>(x => x.Description),
            LinkEntities = {
                new LinkEntity<Contact>(x => x.Contact_Emails){
                    Columns = new ColumnSet<Email>(x => x.Subject)
                    Conditions = {
                        new ConditionExpression<Email>(x => x.AttachmentCount, ConditionOperator.GreaterThan, 0)
                    }
                }
            }
        }
    }
}.RetrieveMultiple(service).Entities;
```  