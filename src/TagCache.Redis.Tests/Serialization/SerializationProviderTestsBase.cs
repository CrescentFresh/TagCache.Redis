using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using TagCache.Redis.Interfaces;
using TagCache.Redis.Serialization;

namespace TagCache.Redis.Tests.Serialization
{
    public abstract class SerializationProviderTestsBase
    {
        protected static RedisCacheItem<TestObject> CreateTestObject()
        {
            var value = new RedisCacheItem<TestObject>
            {
                Expires = new DateTime(2014, 01, 02),
                Key = "XmlSerializationProviderTests.Key",
                Value = new TestObject
                {
                    Bar = "Hello",
                    Foo = "World",
                    Score = 789001,
                },
                Tags = new List<string> { "tag1", "tag2", "tag3" }
            };

            value.Value.SomeList = new List<string>();
            for (int i = 0; i < 20; i++)
            {
                value.Value.SomeList.Add(Guid.NewGuid().ToString());
            } 

            return value;
        }
         
        protected abstract ISerializationProvider GetSerializer();


        [Test]
        public void Serialize_RedisCacheItem_ReturnsString()
        {
            var value = CreateTestObject();

            var serializer = GetSerializer();

            var result = serializer.Serialize(value);

            Assert.IsNotNull(result);
        }


        [Test]
        public void Deserialize_SerializedString_ReturnsRedisCacheItem()
        {
            var value = CreateTestObject();

            var serializer = GetSerializer();

            var serialized = serializer.Serialize(value);

            var result = serializer.Deserialize<RedisCacheItem<TestObject>>(serialized);

            Assert.IsNotNull(result);
            Assert.AreEqual(value.Expires, result.Expires);
            Assert.AreEqual(value.Key, result.Key);
            Assert.NotNull(result.Value);
            Assert.AreEqual(value.Value.Foo, result.Value.Foo);
            Assert.IsTrue((value.Tags.Count() == result.Tags.Count()) && !value.Tags.Except(result.Tags).Any());
        }


        [Category("Benchmarks")]
        [Test]
        public void BenchmarkSerialize()
        {
            int count = 1000;

            var value = CreateTestObject();

            var serializer = GetSerializer();

            var serialized = serializer.Serialize(value); // warmup

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            for (int i = 0; i < count; i++)
            {
                serialized = serializer.Serialize(value);
            }
            stopwatch.Stop();

            Console.Write("{0} items serialised in {1}ms = {2}ms/item", count, stopwatch.ElapsedMilliseconds, (double)stopwatch.ElapsedMilliseconds / count);
        }


        [Category("Benchmarks")]
        [Test]
        public void BenchmarkDeserialize()
        {
            int count = 1000;

            var value = CreateTestObject();

            var serializer = GetSerializer();

            var serialized = serializer.Serialize(value); // warmup
            var deserialized = serializer.Deserialize<RedisCacheItem<TestObject>>(serialized); // warmup

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            for (int i = 0; i < count; i++)
            {
                deserialized = serializer.Deserialize<RedisCacheItem<TestObject>>(serialized); // warmup
            }
            stopwatch.Stop();

            Console.Write("{0} items deserialised in {1}ms = {2}ms/item", count, stopwatch.ElapsedMilliseconds, (double)stopwatch.ElapsedMilliseconds / count);
        }

    }
}