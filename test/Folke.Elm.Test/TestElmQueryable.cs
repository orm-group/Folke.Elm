﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Diagnostics;
using Folke.Elm.Fluent;
using Folke.Elm.Mapping;
using Xunit;
using System.Linq;
using Moq;

namespace Folke.Elm.Test
{
    public class TestElmQueryable
    {
        private readonly ElmQueryable<TestPoco> queryable;
        private readonly Mock<IFolkeCommand> commandMock;
        private readonly ElmQueryProvider provider;
        private readonly Mock<DbDataReader> dbReaderMock;
        private Mock<IDatabaseDriver> driverMock;

        public class TestPoco
        {
            [Key]
            public int Id { get; set; }
            public string Name { get; set; }
            public decimal Decimal { get; set; }
        }

        public class TestOther
        {
            [Key]
            public int Id { get; set; }
            public TestPoco TestPoco { get; set; }
            public string Value { get; set; }
        }

        public TestElmQueryable()
        {
            driverMock = new Mock<IDatabaseDriver>();
            var mapper = new Mapper();
            var connection = new Mock<IFolkeConnection>(); //  FolkeConnection.Create(driverMock.Object, mapper);
            connection.Setup(x => x.Mapper).Returns(mapper);
            connection.Setup(x => x.Driver).Returns(driverMock.Object);
            commandMock = new Mock<IFolkeCommand>();
            dbReaderMock = new Mock<DbDataReader>();
            commandMock.Setup(x => x.ExecuteReader()).Returns(dbReaderMock.Object);
            commandMock.SetupProperty(x => x.CommandText);
            connection.Setup(x => x.CreateCommand(It.IsAny<string>(), It.IsAny<object[]>())).Returns(
                (string text, object[] parameters) =>
                {
                    commandMock.Object.CommandText = text;
                    return commandMock.Object;
                });
            provider = new ElmQueryProvider(connection.Object);
            queryable = new ElmQueryable<TestPoco>(null, provider);
        }

        [Fact]
        public void Where()
        {
            // Arrange

            // Act
            List<TestPoco> result = queryable.Where(x => x.Name == "Toto").ToList();

            // Assert
            Assert.Empty(result);
            Assert.Equal("SELECT \"t\".\"Id\", \"t\".\"Name\", \"t\".\"Decimal\" FROM \"TestPoco\" AS t WHERE( \"t\".\"Name\"= @Item0)", commandMock.Object.CommandText);
        }

        [Fact]
        public void ToList()
        {
            // Arrange

            // Act
            List<TestPoco> result = queryable.ToList();

            // Assert
            Assert.Empty(result);
            Assert.Equal("SELECT \"t\".\"Id\", \"t\".\"Name\", \"t\".\"Decimal\" FROM \"TestPoco\" AS t", commandMock.Object.CommandText);
        }

        [Fact]
        public void SkipAndTake()
        {
            // Arrange

            // Act
            List<TestPoco> result = queryable.OrderBy(x => x.Name).Skip(10).Take(15).ToList();

            // Assert
            Assert.Empty(result);
            Assert.Equal("SELECT \"t\".\"Id\", \"t\".\"Name\", \"t\".\"Decimal\" FROM \"TestPoco\" AS t ORDER BY  \"t\".\"Name\" LIMIT @Item0, @Item1", commandMock.Object.CommandText);
        }

        [Fact]
        public void Count()
        {
            // Arrange
            dbReaderMock.Setup(x => x.Read()).Returns(true);
            driverMock.Setup(x => x.ConvertReaderValueToValue(dbReaderMock.Object, typeof(int), 0)).Returns(18);

            // Act
            int result = queryable.Count();

            // Assert
            Assert.Equal(18, result);
            Assert.Equal("SELECT COUNT(*) FROM \"TestPoco\" AS t0", commandMock.Object.CommandText);
        }

        [Fact(Skip = "Not implemented")]
        public void Join()
        {
            // Arrange
            var otherQueryable = new ElmQueryable<TestOther>(provider);

            // Act
            var result = (from testPoco in queryable
                                     join other in otherQueryable on testPoco equals other.TestPoco
                                     select new { testPoco, other }).ToList();

            // Assert
            Assert.Empty(result);
            Assert.Equal("SELECT \"t\".\"Id\", \"t\".\"Name\", \"t\".\"Decimal", commandMock.Object.CommandText);
        }
    }
}
