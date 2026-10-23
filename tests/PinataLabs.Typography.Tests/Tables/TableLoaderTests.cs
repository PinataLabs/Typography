// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables;

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
        public async Task AllNamedTablesAreRegistered(Type type, string name)
        {
            var tl = new TableLoader();
            await Assert.That(tl.RegisteredTypes()).Contains(type);
            await Assert.That(tl.GetTag(type)).IsEqualTo(name);
        }

        [Test]
        public async Task DefaultIsNotNull() => await Assert.That(TableLoader.Default).IsNotNull();

        [Test]
        public async Task TryingToLoadUnregisteredTagReturnsUnknownTable()
        {
            var loader = new TableLoader();

            string tag = Guid.NewGuid().ToString();
            Table result = loader.Load(tag, null);

            UnknownTable table = await Assert.That(result).IsTypeOf<UnknownTable>();
            await Assert.That(table.Name).IsEqualTo(tag);
        }

        [Test]
        public async Task NullForUnknownTypes()
        {
            var loader = new TableLoader();
            string tag = loader.GetTag(typeof(TableLoaderTests));
            await Assert.That(tag).IsNull();
        }
    }
}
