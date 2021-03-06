﻿using System.Collections.Generic;
using System.Linq;
using FastTests;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Transformers;
using Xunit;

namespace SlowTests.Issues
{
    public class RavenDB_2793 : RavenTestBase
    {
        private class Person
        {
            public string FirstName { get; set; }

            public string LastName { get; set; }
        }

        private class People_FirstName : AbstractTransformerCreationTask<Person>
        {
            public class Result
            {
                public string FirstName { get; set; }
            }

            public People_FirstName()
            {
                TransformResults = people => from person in people
                                             select new
                                             {
                                                 FirstName = person.FirstName + Parameter("value").Value<int>()
                                             };
            }
        }

        [Fact]
        public void TransformerParametersShouldWorkWithLazyLoadWithTransformer()
        {
            using (var store = GetDocumentStore())
            {
                new People_FirstName().Execute(store);

                using (var session = store.OpenSession())
                {
                    session.Store(new Person { FirstName = "John" });
                    session.Store(new Person { FirstName = "Alfred" });

                    session.SaveChanges();
                }

                using (var session = store.OpenSession())
                {
                    var lazy1 = session
                        .Advanced
                        .Lazily
                        .Load<People_FirstName, Person>("people/1-A", configure => configure.AddTransformerParameter("value", 10));

                    var value1 = lazy1.Value;

                    Assert.Equal("John10", value1.FirstName);

                    var lazy2 = session
                        .Advanced
                        .Lazily
                        .Load<People_FirstName, Person>(new List<string> { "people/1-A", "people/2-A" }, configure => configure.AddTransformerParameter("value", 15));

                    var value2 = lazy2.Value;

                    Assert.Equal("John15", value2["people/1-A"].FirstName);
                    Assert.Equal("Alfred15", value2["people/2-A"].FirstName);
                }
            }
        }

    }
}
