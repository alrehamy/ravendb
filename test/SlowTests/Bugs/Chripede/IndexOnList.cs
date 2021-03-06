using System;
using System.Collections.Generic;
using System.Linq;
using FastTests;
using Raven.Client.Documents.Indexes;
using Xunit;

namespace SlowTests.Bugs.Chripede
{

    public class IndexOnList : RavenTestBase
    {
        private class Document
        {
            public string Id { get; set; }

            public IList<string> List { get; set; }
        }

        private class Document_Index : AbstractIndexCreationTask<Document>
        {
            public Document_Index()
            {
                Map = docs => from doc in docs
                              select new
                              {
                                  doc.List,
                              };
            }
        }

        [Fact]
        public void CanIndexAndQueryOnList()
        {

            using (var store = GetDocumentStore())
            {

                var task = (AbstractIndexCreationTask)Activator.CreateInstance(typeof(Document_Index));

                task.Execute(store);

                using (var session = store.OpenSession())
                {
                    session.Store(new Document
                    {
                        List = new List<string>() { "test1", "test2", "test3" }
                    });

                    session.SaveChanges();
                }

                using (var session = store.OpenSession())
                {
                    var result = session.Query<Document, Document_Index>()
                        .Customize(customization => customization.WaitForNonStaleResultsAsOfNow())
                        .Where(x => x.List.Any(s => s == "test1"))
                        .ToList();

                    Assert.Equal(1, result.Count);
                }

                //// Works when not using the index
                //using (var session = store.OpenSession())
                //{
                //    var result = session.Query<Document>()
                //        .Customize(customization => customization.WaitForNonStaleResultsAsOfNow())
                //        .Where(x => x.List.Any(s => s == "test1"))
                //        .ToList();

                //    Assert.Equal(1, result.Count);
                //}

            }
        }
    }

}
