// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;

namespace System.Collections.Tests
{
    public class OrderedDictionary_IDictionary_NonGeneric_Tests : IDictionary_NonGeneric_Tests
    {
        #region IDictionary Helper Methods
        protected override bool Enumerator_Current_UndefinedOperation_Throws => false;
        protected override bool Enumerator_Empty_Current_UndefinedOperation_Throw => true;
        protected override bool Enumerator_Empty_UsesSingletonInstance => true;
        protected override bool Enumerator_Empty_ModifiedDuringEnumeration_ThrowsInvalidOperationException => false;
        protected override ModifyOperation ModifyEnumeratorThrows => ModifyOperation.Add | ModifyOperation.Insert | ModifyOperation.Remove | ModifyOperation.Clear;
        protected override bool SupportsSerialization => false;

        protected override IDictionary NonGenericIDictionaryFactory()
        {
            return new OrderedDictionary<string, string>();
        }

        /// <summary>
        /// Creates an object that is dependent on the seed given. The object may be either
        /// a value type or a reference type, chosen based on the value of the seed.
        /// </summary>
        protected override object CreateTKey(int seed)
        {
            int stringLength = seed % 10 + 5;
            Random rand = new Random(seed);
            byte[] bytes = new byte[stringLength];
            rand.NextBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Creates an object that is dependent on the seed given. The object may be either
        /// a value type or a reference type, chosen based on the value of the seed.
        /// </summary>
        protected override object CreateTValue(int seed) => CreateTKey(seed);

        #endregion

        #region Ordering tests

        [Fact]
        public void Ordering_AddInsertRemoveClear_ExpectedOrderResults()
        {
            OrderedDictionary<int, int> d = [];

            d.Add(1, 1);
            d.Add(2, 2);
            d.Add(3, 3);
            Assert.Equal(new[] { 1, 2, 3 }, d.Keys);
            Assert.Equal(new[] { 1, 2, 3 }, d.Values);
            Assert.Equal(new[] { KeyValuePair.Create(1, 1), KeyValuePair.Create(2, 2), KeyValuePair.Create(3, 3) }, d);

            d.Remove(2);
            Assert.Equal(new[] { 1, 3 }, d.Keys);
            Assert.Equal(new[] { 1, 3 }, d.Values);
            Assert.Equal(new[] { KeyValuePair.Create(1, 1), KeyValuePair.Create(3, 3) }, d);

            d.Add(4, 4);
            Assert.Equal(new[] { 1, 3, 4 }, d.Keys);
            Assert.Equal(new[] { 1, 3, 4 }, d.Values);
            Assert.Equal(new[] { KeyValuePair.Create(1, 1), KeyValuePair.Create(3, 3), KeyValuePair.Create(4, 4) }, d);

            d.Insert(0, 5, 5);
            Assert.Equal(new[] { 5, 1, 3, 4 }, d.Keys);
            Assert.Equal(new[] { 5, 1, 3, 4 }, d.Values);
            Assert.Equal(new[] { KeyValuePair.Create(5, 5), KeyValuePair.Create(1, 1), KeyValuePair.Create(3, 3), KeyValuePair.Create(4, 4) }, d);

            d.RemoveAt(2);
            Assert.Equal(new[] { 5, 1, 4 }, d.Keys);
            Assert.Equal(new[] { 5, 1, 4 }, d.Values);
            Assert.Equal(new[] { KeyValuePair.Create(5, 5), KeyValuePair.Create(1, 1), KeyValuePair.Create(4, 4) }, d);

            d.Add(6, 6);
            Assert.Equal(new[] { 5, 1, 4, 6 }, d.Keys);
            Assert.Equal(new[] { 5, 1, 4, 6 }, d.Values);
            Assert.Equal(new[] { KeyValuePair.Create(5, 5), KeyValuePair.Create(1, 1), KeyValuePair.Create(4, 4), KeyValuePair.Create(6, 6) }, d);

            d.Clear();
            Assert.Empty(d.Keys);
            Assert.Empty(d.Values);
            Assert.Empty(d);

            d.Add(7, 7);
            d.Add(9, 9);
            d.Add(8, 8);
            Assert.Equal(new[] { 7, 9, 8 }, d.Keys);
            Assert.Equal(new[] { 7, 9, 8 }, d.Values);
            Assert.Equal(new[] { KeyValuePair.Create(7, 7), KeyValuePair.Create(9, 9), KeyValuePair.Create(8, 8) }, d);
        }

        #endregion

        #region Clear

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(17)]
        [InlineData(100)]
        public void Clear_CollidingEntries_CanRefillWithDifferentValues(int count)
        {
            var comparer = EqualityComparer<int>.Create((x, y) => x == y, _ => 0);
            var dictionary = new OrderedDictionary<int, (int, long)>(count, comparer);
            var entries = Enumerable.Range(0, count).Select(i => KeyValuePair.Create(i, (i, ~(long)i))).ToArray();
            foreach (var entry in entries)
            {
                dictionary.Add(entry.Key, entry.Value);
            }
            int capacity = dictionary.Capacity;

            for (int cycle = 0; cycle < 3; cycle++)
            {
                for (int i = dictionary.Count - 1; i >= 0; i -= 2)
                {
                    dictionary.RemoveAt(i);
                }

                dictionary.Clear();
                Assert.Empty(dictionary);
                Assert.Empty(dictionary.Keys);
                Assert.Empty(dictionary.Values);
                Assert.Equal(capacity, dictionary.Capacity);
                var oldEntries = entries;
                entries = Enumerable.Range((cycle + 1) * count, count).Select(i => KeyValuePair.Create(i, (i, ~(long)i))).ToArray();
                foreach (var entry in entries)
                {
                    dictionary.Insert(0, entry.Key, entry.Value);
                    Assert.Equal(entry.Value, dictionary[entry.Key]);
                }

                Assert.Equal(Enumerable.Reverse(entries), dictionary);
                foreach (var entry in oldEntries)
                {
                    Assert.False(dictionary.TryGetValue(entry.Key, out _));
                }
            }
        }

        [ConditionalTheory(typeof(PlatformDetection), nameof(PlatformDetection.IsPreciseGcSupported))]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void Clear_ReleasesKeyAndValueReferences(bool referenceInKey, bool wrapInStruct)
        {
            VerifyReferenceRelease(referenceInKey, wrapInStruct, removeIndex: -1);
        }

        #endregion

        #region RemoveAt

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void RemoveAt_CollidingEntries_PreservesOtherEntriesAndCanInsert(int index)
        {
            var comparer = EqualityComparer<int>.Create((x, y) => x == y, _ => 0);
            var dictionary = new OrderedDictionary<int, (int, long)>(comparer);
            var expected = Enumerable.Range(0, 3).Select(i => KeyValuePair.Create(i, (i, ~(long)i))).ToList();
            foreach (var entry in expected)
            {
                dictionary.Add(entry.Key, entry.Value);
            }
            var removed = expected[index];
            var enumerator = dictionary.GetEnumerator();

            dictionary.RemoveAt(index);
            expected.RemoveAt(index);
            Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
            Assert.Equal(expected, dictionary);
            Assert.Equal(expected.Select(entry => entry.Key), dictionary.Keys);
            Assert.Equal(expected.Select(entry => entry.Value), dictionary.Values);
            Assert.False(dictionary.TryGetValue(removed.Key, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => dictionary.GetAt(dictionary.Count));

            dictionary.Insert(index, 99, (99, -100L));
            expected.Insert(index, KeyValuePair.Create(99, (99, -100L)));
            Assert.Equal(expected, dictionary);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i], dictionary.GetAt(i));
                Assert.Equal(expected[i].Value, dictionary[expected[i].Key]);
            }
            Assert.False(dictionary.ContainsKey(removed.Key));
        }

        [ConditionalTheory(typeof(PlatformDetection), nameof(PlatformDetection.IsPreciseGcSupported))]
        [InlineData(0, false, false)]
        [InlineData(0, false, true)]
        [InlineData(0, true, false)]
        [InlineData(0, true, true)]
        [InlineData(1, false, false)]
        [InlineData(1, false, true)]
        [InlineData(1, true, false)]
        [InlineData(1, true, true)]
        [InlineData(2, false, false)]
        [InlineData(2, false, true)]
        [InlineData(2, true, false)]
        [InlineData(2, true, true)]
        public void RemoveAt_ReleasesOnlyRemovedReferences(int index, bool referenceInKey, bool wrapInStruct)
        {
            VerifyReferenceRelease(referenceInKey, wrapInStruct, index);
        }

        private static void VerifyReferenceRelease(bool referenceInKey, bool wrapInStruct, int removeIndex)
        {
            if (referenceInKey)
            {
                if (wrapInStruct)
                {
                    Verify<(int, (object, int)), int>((value, index) => KeyValuePair.Create((index, (value, index)), index));
                }
                else
                {
                    Verify<object, int>((value, index) => KeyValuePair.Create(value, index));
                }
            }
            else
            {
                if (wrapInStruct)
                {
                    Verify<int, (int, (object, int))>((value, index) => KeyValuePair.Create(index, (index, (value, index))));
                }
                else
                {
                    Verify<int, object>((value, index) => KeyValuePair.Create(index, value));
                }
            }

            void Verify<TKey, TValue>(Func<object, int, KeyValuePair<TKey, TValue>> create) where TKey : notnull
            {
                var dictionary = new OrderedDictionary<TKey, TValue>();
                WeakReference[] references = Enumerable.Range(0, 3).Select(i => AddReference(dictionary, create, i)).ToArray();
                GC.Collect();
                Assert.All(references, reference => Assert.True(reference.IsAlive));

                if (removeIndex < 0)
                {
                    dictionary.Clear();
                }
                else
                {
                    dictionary.RemoveAt(removeIndex);
                }

                Assert.Equal(removeIndex < 0 ? 0 : 2, dictionary.Count);
                Assert.True(SpinWait.SpinUntil(() =>
                {
                    GC.Collect();
                    return removeIndex < 0 ? references.All(reference => !reference.IsAlive) : !references[removeIndex].IsAlive;
                }, 30_000));
                if (removeIndex >= 0)
                {
                    for (int i = 0; i < references.Length; i++)
                    {
                        Assert.Equal(i != removeIndex, references[i].IsAlive);
                    }
                }
                GC.KeepAlive(dictionary);
            }

            // Keep the objects out of the caller's stack during the collection check.
            [MethodImpl(MethodImplOptions.NoInlining)]
            static WeakReference AddReference<TKey, TValue>(OrderedDictionary<TKey, TValue> dictionary, Func<object, int, KeyValuePair<TKey, TValue>> create, int index) where TKey : notnull
            {
                object value = new object();
                var entry = create(value, index);
                dictionary.Add(entry.Key, entry.Value);
                return new WeakReference(value);
            }
        }

        #endregion

        #region IDictionary tests

        [Fact]
        public void IDictionary_NonGeneric_ItemSet_NullValueWhenDefaultValueIsNonNull()
        {
            IDictionary dictionary = new OrderedDictionary<string, int>();
            Assert.Throws<ArgumentNullException>(() => dictionary[GetNewKey(dictionary)] = null);
        }

        [Fact]
        public void IDictionary_NonGeneric_ItemGet_KeyOfWrongType()
        {
            IDictionary dictionary = new OrderedDictionary<string, string>();
            dictionary.Add("key", "value");
            Assert.Null(dictionary[42]);
            Assert.Null(dictionary[KeyValuePair.Create("key", "value")]);
        }

        [Fact]
        public void IDictionary_NonGeneric_ItemSet_KeyOfWrongType()
        {
            if (!IsReadOnly)
            {
                IDictionary dictionary = new OrderedDictionary<string, string>();
                AssertExtensions.Throws<ArgumentException>("key", () => dictionary[23] = CreateTValue(12345));
                Assert.Empty(dictionary);
            }
        }

        [Fact]
        public void IDictionary_NonGeneric_ItemSet_ValueOfWrongType()
        {
            if (!IsReadOnly)
            {
                IDictionary dictionary = new OrderedDictionary<string, string>();
                object missingKey = GetNewKey(dictionary);
                AssertExtensions.Throws<ArgumentException>("value", () => dictionary[missingKey] = 324);
                Assert.Empty(dictionary);
            }
        }

        [Fact]
        public void IDictionary_NonGeneric_Add_KeyOfWrongType()
        {
            if (!IsReadOnly)
            {
                IDictionary dictionary = new OrderedDictionary<string, string>();
                object missingKey = 23;
                AssertExtensions.Throws<ArgumentException>("key", () => dictionary.Add(missingKey, CreateTValue(12345)));
                Assert.Empty(dictionary);

                dictionary = new OrderedDictionary<string, int>();
                AssertExtensions.Throws<ArgumentNullException>("value", () => dictionary.Add("key", null));
                Assert.Empty(dictionary);
            }
        }

        [Fact]
        public void IDictionary_NonGeneric_Add_ValueOfWrongType()
        {
            if (!IsReadOnly)
            {
                IDictionary dictionary = new OrderedDictionary<string, string>();
                object missingKey = GetNewKey(dictionary);
                AssertExtensions.Throws<ArgumentException>("value", () => dictionary.Add(missingKey, 324));
                Assert.Empty(dictionary);
            }
        }

        [Fact]
        public void IDictionary_NonGeneric_Add_NullValueWhenDefaultTValueIsNonNull()
        {
            if (!IsReadOnly)
            {
                IDictionary dictionary = new OrderedDictionary<string, int>();
                object missingKey = GetNewKey(dictionary);
                Assert.Throws<ArgumentNullException>(() => dictionary.Add(missingKey, null));
                Assert.Empty(dictionary);
            }
        }

        [Fact]
        public void IDictionary_NonGeneric_Contains_KeyOfWrongType()
        {
            if (!IsReadOnly)
            {
                IDictionary dictionary = new OrderedDictionary<string, int>();
                Assert.False(dictionary.Contains(1));
            }
        }

        [Fact]
        public void IDictionary_NonGeneric_Remove_KeyOfWrongType()
        {
            if (!IsReadOnly)
            {
                IDictionary dictionary = new OrderedDictionary<string, int>();
                dictionary.Remove(1); // ignored
                Assert.Empty(dictionary);
            }
        }

        [Fact]
        public void CantAcceptDuplicateKeysFromSourceDictionary()
        {
            Dictionary<string, int> source = new Dictionary<string, int> { { "a", 1 }, { "A", 1 } };
            AssertExtensions.Throws<ArgumentException>("key", () => new OrderedDictionary<string, int>(source, StringComparer.OrdinalIgnoreCase));
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public override void IDictionary_NonGeneric_IDictionaryEnumerator_Current_AfterEndOfEnumerable_UndefinedBehavior(int count)
        {
            if (count != 0)
            {
                // Different undefined behavior for IDictionary.GetEnumerator when empty than for ICollection.GetEnumerator,
                // as the former doesn't use a singleton.
                base.IDictionary_NonGeneric_IDictionaryEnumerator_Current_AfterEndOfEnumerable_UndefinedBehavior(count);
            }
        }

        #endregion

        #region ICollection tests

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ICollection_NonGeneric_CopyTo_ArrayOfIncorrectKeyValuePairType(int count)
        {
            ICollection collection = NonGenericICollectionFactory(count);
            KeyValuePair<string, int>[] array = new KeyValuePair<string, int>[count * 3 / 2];
            AssertExtensions.Throws<ArgumentException>("array", null, () => collection.CopyTo(array, 0));
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void ICollection_NonGeneric_CopyTo_ArrayOfCorrectKeyValuePairType(int count)
        {
            ICollection collection = NonGenericICollectionFactory(count);
            KeyValuePair<string, string>[] array = new KeyValuePair<string, string>[count];
            collection.CopyTo(array, 0);
            int i = 0;
            foreach (object obj in collection)
            {
                Assert.Equal(array[i++], obj);
            }
        }

        #endregion
    }
}
