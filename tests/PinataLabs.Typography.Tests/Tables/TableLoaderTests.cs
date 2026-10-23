// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PinataLabs.Typography.Tables;

using Xunit;

namespace PinataLabs.Typography.Tests.Tables
{
    public class TableLoaderTests
    {
        public static IEnumerable<object[]> RegisterableTableTypes
        {
            get
            {
                System.Reflection.TypeInfo tableTypeInfo = typeof(Table).GetTypeInfo();

                return
                    typeof(Table).GetTypeInfo()
                        .Assembly.DefinedTypes
                        .Where(x => tableTypeInfo.IsAssignableFrom(x))
                        .Select(x => new object[] { x.AsType(), x.GetField("TableName", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(null) })
                        .Where(x => x[1] != null);
            }
        }

        [Test]
        [MethodDataSource(nameof(RegisterableTableTypes))]
        public void AllNamedTablesAreRegistered(Type type, string name)
        {
            var tl = new TableLoader();
            Assert.Contains(type, tl.RegisteredTypes());
            Assert.Equal(name, tl.GetTag(type));
        }

        [Test]
        public void DefaultIsNotNull() => Assert.NotNull(TableLoader.Default);

        [Test]
        public void TryingToLoadUnregisteredTagReturnsUnknownTable()
        {
            var loader = new TableLoader();

            string tag = Guid.NewGuid().ToString();
            Table result = loader.Load(tag, null);

            UnknownTable table = Assert.IsType<UnknownTable>(result);
            Assert.Equal(tag, table.Name);
        }

        [Test]
        public void NullForUnknownTypes()
        {
            var loader = new TableLoader();
            string tag = loader.GetTag(typeof(TableLoaderTests));
            Assert.Null(tag);
        }
    }
}
