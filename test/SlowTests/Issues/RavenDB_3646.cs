using System.Linq;
using FastTests;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Indexes.Spatial;
using Raven.Client.Documents.Session;
using Xunit;

namespace SlowTests.Issues
{
    public class RavenDB_3646 : RavenTestBase
    {
        [Fact(Skip = "RavenDB-5988")]
        public void QueryWithCustomize()
        {
            using (var store = GetDocumentStore())
            {
                StoreData(store);
                store.ExecuteIndex(new Events_SpatialIndex());
                WaitForIndexing(store);
                using (var session = store.OpenSession())
                {
                    QueryStatistics stats;
                    var rq = session.Query<Events_SpatialIndex.ReduceResult, Events_SpatialIndex>()
                        .Statistics(out stats)
                        .Customize(
                            x => x.WithinRadiusOf("Coordinates", 10000, 1, 1,
                                SpatialUnits.Miles));
                    var t = 0;

                    using (var enumerator = session.Advanced.Stream(rq.ProjectFromIndexFieldsInto<Event>()))
                    {
                        while (enumerator.MoveNext())
                        {
                            t++;
                        }
                    }

                    Assert.Equal(300, t);
                }
            }
        }

        [Fact(Skip = "RavenDB-5988")]
        public void QueryWithoutCustomize()
        {
            using (var store = GetDocumentStore())
            {
                StoreData(store);
                store.ExecuteIndex(new Events_SpatialIndex());
                WaitForIndexing(store);
                using (var session = store.OpenSession())
                {
                    QueryStatistics stats;
                    var rq = session.Query<Events_SpatialIndex.ReduceResult, Events_SpatialIndex>()
                        .Statistics(out stats);

                    using (var enumerator = session.Advanced.Stream(rq.ProjectFromIndexFieldsInto<Event>()))
                    {
                        var t = 0;
                        while (enumerator.MoveNext())
                        {
                            t++;
                        }
                        Assert.Equal(300, t);
                    }
                }
            }
        }

        private class Events_SpatialIndex : AbstractIndexCreationTask<Event, Events_SpatialIndex.ReduceResult>
        {
            public class ReduceResult
            {
                public string Name { get; set; }
            }

            public Events_SpatialIndex()
            {
                Map = events => from e in events
                                select new
                                {
                                    Name = e.Name,
                                    __ = SpatialGenerate("Coordinates", e.Latitude, e.Longitude)
                                };
            }
        }

        private class Event
        {
            public string Name { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
        }

        private void StoreData(IDocumentStore store)
        {
            using (var session = store.OpenSession())
            {
                for (int i = 0; i < 300; i++)
                    session.Store(new Event { Name = "e1" + i, Latitude = 1, Longitude = 1 });
                session.SaveChanges();
            }
        }
    }
}
