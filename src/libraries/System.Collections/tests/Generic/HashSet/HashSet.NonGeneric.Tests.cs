// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;
using Xunit;

namespace System.Collections.Tests
{
    public static class HashSet_NonGeneric_Tests
    {
        [Fact]
        public static void HashSet_CopyConstructor_ShouldWorkWithRandomizedEffectiveComparer()
        {
            HashSet<string> set = CreateCopyWithRandomizedComparer(new HashSet<string>() { "a", "b" });
            Assert.True(set.Contains("a"));

            HashSet<string> copiedSet = new(set);
            Assert.True(copiedSet.Contains("a"));

            static HashSet<string> CreateCopyWithRandomizedComparer(HashSet<string> set)
            {
                // To reproduce the bug, we need a HashSet<string> instance that has fallen back to
                // the randomized comparer. This typically happens when there are many collisions but
                // it can also happen when the set is serialized and deserialized via ISerializable.
                // For consistent results and to avoid brute forcing collisions, use the latter approach.

                SerializationInfo info = new(typeof(HashSet<string>), new FormatterConverter());
                StreamingContext context = new(StreamingContextStates.All);
                set.GetObjectData(info, context);
  
                HashSet<string> copiedSet = (HashSet<string>)Activator.CreateInstance(typeof(HashSet<string>), BindingFlags.NonPublic | BindingFlags.Instance, null, [info, context], null);
                copiedSet.OnDeserialization(null);
                return copiedSet;
            }
        }

        [Theory]
        [InlineData(10)]
        [InlineData(1000)]
        [InlineData(1000_000)]
        public static void InsertionOpsOnly_Enumeration_PreservesInsertionOrder(int count)
        {
            var set = new HashSet<string>();
            for (int i = 0; i < count; i++)
            {
                set.Add(i.ToString());
            }

            int j = 0;
            foreach (string elem in set)
            {
                Assert.Equal(j.ToString(), elem);
                j++;
            }
        }
        #region Clear

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(100)]
        public static void Clear_CollidingValues_CanRefillWithDifferentValues(int count)
        {
            var comparer = EqualityComparer<(int, long)>.Create((x, y) => x == y, _ => 0);
            var set = new HashSet<(int, long)>(count, comparer);
            var items = Enumerable.Range(0, count).Select(i => (i, ~(long)i)).ToArray();
            set.UnionWith(items);
            int capacity = set.EnsureCapacity(0);

            for (int cycle = 0; cycle < 3; cycle++)
            {
                for (int i = 0; i < items.Length; i += 2)
                {
                    Assert.True(set.Remove(items[i]));
                }

                set.Clear();
                Assert.Empty(set);
                Assert.Equal(0, set.Count);
                Assert.Equal(capacity, set.EnsureCapacity(0));

                foreach (var item in items)
                {
                    Assert.False(set.Contains(item));
                    Assert.False(set.TryGetValue(item, out _));
                }

                var oldItems = items;
                items = Enumerable.Range((cycle + 1) * count, count).Select(i => (i, ~(long)i)).ToArray();
                foreach (var item in items)
                {
                    Assert.True(set.Add(item));
                    Assert.False(set.Add(item));
                    Assert.True(set.TryGetValue(item, out var actual));
                    Assert.Equal(item, actual);
                }

                Assert.Equal(items, set.OrderBy(item => item.Item1));
                Assert.All(oldItems, item => Assert.False(set.Contains(item)));
            }
        }

        [ConditionalTheory(typeof(PlatformDetection), nameof(PlatformDetection.IsPreciseGcSupported))]
        [InlineData(false)]
        [InlineData(true)]
        public static void Clear_ReleasesReferences(bool wrapInStruct)
        {
            if (wrapInStruct)
            {
                Verify<(int, (object, int))>(value => (1, (value, 2)));
            }
            else
            {
                Verify<object>(value => value);
            }

            static void Verify<T>(Func<object, T> create)
            {
                var set = new HashSet<T>();
                WeakReference[] references = Enumerable.Range(0, 3).Select(_ => AddReference(set, create)).ToArray();
                GC.Collect();
                Assert.All(references, reference => Assert.True(reference.IsAlive));

                set.Clear();
                Assert.Empty(set);
                Assert.True(SpinWait.SpinUntil(() =>
                {
                    GC.Collect();
                    return references.All(reference => !reference.IsAlive);
                }, 30_000));
                GC.KeepAlive(set);
            }

            // Keep the objects out of the caller's stack during the collection check.
            [MethodImpl(MethodImplOptions.NoInlining)]
            static WeakReference AddReference<T>(HashSet<T> set, Func<object, T> create)
            {
                object value = new object();
                Assert.True(set.Add(create(value)));
                return new WeakReference(value);
            }
        }

        #endregion
    }
}
