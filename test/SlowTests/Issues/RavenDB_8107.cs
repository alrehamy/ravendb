﻿using System;
using FastTests;
using FastTests.Server.Basic.Entities;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Queries;
using Raven.Client.Exceptions;
using Xunit;

namespace SlowTests.Issues
{
    public class RavenDB_8107 : RavenTestBase
    {
        [Fact]
        public void Can_patch_by_dynamic_collection_query()
        {
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenSession())
                {
                    session.Store(new Order(), "orders/1");
                    
                    session.SaveChanges();
                }

                var operation = store.Operations.Send(new PatchByQueryOperation(
                        new IndexQuery { Query = "FROM Orders" },
                        new PatchRequest { Script = @"this.Company = 'HR';" }));

                operation.WaitForCompletion(TimeSpan.FromSeconds(15));

                using (var session = store.OpenSession())
                {
                    var order = session.Load<Order>("orders/1");

                    Assert.Equal("HR", order.Company);
                }
            }
        }

        [Fact]
        public void Patching_by_dynamic_collection_query_with_filtering_should_throw()
        {
            using (var store = GetDocumentStore())
            {
                var ex = Assert.Throws<BadRequestException>(() => store.Operations.Send(new PatchByQueryOperation(
                    new IndexQuery { Query = "FROM Orders WHERE Company = 'companies/1'" },
                    new PatchRequest { Script = @"this.Company = 'HR';" })));

                Assert.Contains("Patching documents by a dynamic query is supported only for queries having just FROM clause, e.g. 'FROM Orders'. If you need to perform filtering please issue the query to the static index.", ex.Message);
            }
        }
    }
}
