// -----------------------------------------------------------------------
//  <copyright file="RavenDB_3197.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FastTests;
using FastTests.Utils;
using Raven.Client;
using Raven.Client.Exceptions.Documents.Patching;
using Raven.Client.ServerWide.Operations;
using Raven.Server.Documents.Patch;
using Raven.Server.ServerWide.Context;
using Xunit;

namespace SlowTests.Issues
{
    public class RavenDB_3197 : RavenTestBase
    {
        private class SimpleUser
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
        }

        [Fact]
        public async Task ScriptPatchShouldGenerateNiceException()
        {
            using (var store = GetDocumentStore())
            {
                using (var session = store.OpenSession())
                {
                    session.Store(new SimpleUser { FirstName = "John", LastName = "Smith" });
                    session.SaveChanges();
                }
                var functions = LinuxTestUtils.Dos2Unix(@"exports.a = function(value) { return  b(value); };
exports.b = function(v) { return c(v); }
exports.c = function(v) { throw 'oops'; }
");
                await store.Admin.Server.SendAsync(new ModifyCustomFunctionsOperation(store.Database, functions)).ConfigureAwait(false);

                var database = await GetDocumentDatabaseInstanceFor(store);

                using (database.DocumentsStorage.ContextPool.AllocateOperationContext(out DocumentsOperationContext context))
                using (context.OpenWriteTransaction())
                {
                    var e = Assert.Throws<JavaScriptException>(() =>
                    {
                        var document = database.DocumentsStorage.Get(context, "simpleUsers/1-A");

                        database.Patcher.Apply(context, document, new PatchRequest
                        {
                            Script = LinuxTestUtils.Dos2Unix(@"var s = 1234; 
a(s);")
                        });
                    });

                    Assert.Equal(LinuxTestUtils.Dos2Unix(@"Unable to execute JavaScript: 
var s = 1234; 
a(s);

Error: 
oops
Stacktrace:
c@customFunctions.js:3
b@customFunctions.js:2
a@customFunctions.js:1
apply@main.js:2
anonymous function@main.js:1"), e.Message);
                }
            }
        }
    }
}
