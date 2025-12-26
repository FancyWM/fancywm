using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text.Json;
using System.Threading.Tasks;

using FancyWM.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyWM.Tests.Models
{
    [TestClass]
    public class ObservableFileEntityTests
    {
        private string m_tempDir = null!;

        [TestInitialize]
        public void Setup()
        {
            m_tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(m_tempDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(m_tempDir))
            {
                foreach (var file in Directory.EnumerateFiles(m_tempDir))
                {
                    var fileInfo = new FileInfo(file);
                    fileInfo.Attributes = FileAttributes.None;
                }
                Directory.Delete(m_tempDir, recursive: true);
            }
        }

        [TestMethod]
        public async Task TestCreateNewFileWhenMissing()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var entity = new TestObservableJsonEntity(filePath, () => new TestModel { Value = "default" });

            await entity.SaveAsync(TestModel.Next);

            Assert.IsTrue(File.Exists(filePath));
            var content = File.ReadAllText(filePath);
            Assert.IsTrue(content.Contains("default"));
        }

        [TestMethod]
        public async Task TestReadExistingFile()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var testData = new TestModel { Value = "existing" };
            var json = JsonSerializer.Serialize(testData);
            File.WriteAllText(filePath, json);

            var entity = new TestObservableJsonEntity(filePath, () => new TestModel { Value = "default" });

            var value = await entity.Value.FirstAsync();

            Assert.AreEqual("existing", value.Value);
        }

        [TestMethod]
        public async Task TestSaveAsync()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var entity = new TestObservableJsonEntity(filePath, () => new TestModel { Value = "initial" });

            await entity.SaveAsync(x => new TestModel { Value = "updated" });

            var content = File.ReadAllText(filePath);
            Assert.IsTrue(content.Contains("updated"));
        }

        [TestMethod]
        public async Task TestObservableNotifiesOnSave()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var entity = new TestObservableJsonEntity(filePath, () => new TestModel { Value = "initial" });

            var receivedValues = new List<TestModel>();
            var subscription = entity.Subscribe(receivedValues.Add);

            await entity.SaveAsync(x => new TestModel { Value = "updated" });

            Assert.IsTrue(receivedValues.Count >= 2);
            Assert.AreEqual("initial", receivedValues[0].Value);
            Assert.AreEqual("updated", receivedValues[1].Value);

            subscription.Dispose();
        }

        [TestMethod]
        public async Task TestNoNotifyWhenValueUnchanged()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var entity = new TestObservableJsonEntity(filePath, () => new TestModel { Value = "initial" });

            var receivedValues = new List<TestModel>();
            entity.Subscribe(receivedValues.Add);

            var countBefore = receivedValues.Count;

            await entity.SaveAsync(x => x);

            Assert.AreEqual(countBefore, receivedValues.Count);
        }

        [TestMethod]
        public async Task TestRecreateFileIfDeleted()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var entity = new TestObservableJsonEntity(filePath, () => new TestModel { Value = "restored" });

            File.Delete(filePath);

            await entity.SaveAsync(x => new TestModel { Value = "after_delete" });

            Assert.IsTrue(File.Exists(filePath));
            var content = File.ReadAllText(filePath);
            Assert.IsTrue(content.Contains("after_delete"));
        }

        [TestMethod]
        public async Task TestAtomicWrite()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var entity = new TestObservableJsonEntity(filePath, () => new TestModel { Value = "initial" });

            await entity.SaveAsync(x => new TestModel { Value = "atomic" });

            Assert.IsFalse(File.Exists(filePath + ".tmp"));
        }

        [TestMethod]
        public async Task TestMalformedJsonFallsBackToDefault()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            File.WriteAllText(filePath, "{ invalid json }");

            var entity = new TestObservableJsonEntity(filePath, () => new TestModel { Value = "fallback" });

            var value = await entity.Value.FirstAsync();

            Assert.AreEqual("fallback", value.Value);
        }

        [TestMethod]
        public async Task TestCommentPreservation()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var originalJson = @"{
                // This is a comment
                ""Value"": ""original""
            }";
            File.WriteAllText(filePath, originalJson);

            var entity = new TestObservableJsonEntityWithComments(filePath, () => new TestModel { Value = "default" });

            await entity.SaveAsync(x => new TestModel { Value = "updated" });

            var updatedContent = File.ReadAllText(filePath);
            Assert.IsTrue(updatedContent.Contains("/* This is a comment*/"));
            Assert.IsTrue(updatedContent.Contains("updated"));
        }

        [TestMethod]
        public async Task TestTrailingCommaPreservation()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var originalJson = @"{
                ""Value"": ""original"",
            }";
            File.WriteAllText(filePath, originalJson);

            var entity = new TestObservableJsonEntityWithComments(filePath, () => new TestModel { Value = "default" });

            await entity.SaveAsync(x => new TestModel { Value = "updated" });

            Assert.IsTrue(File.Exists(filePath));
            var updatedContent = File.ReadAllText(filePath);
            Assert.IsTrue(updatedContent.Contains("updated"));
        }

        [TestMethod]
        public async Task TestComplexCommentScenario()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var originalJson = @"{
                // User settings
                ""Value"": ""original"",
                /* Keep this structure */
            }";
            File.WriteAllText(filePath, originalJson);

            var entity = new TestObservableJsonEntityWithComments(filePath, () => new TestModel { Value = "default" });

            await entity.SaveAsync(x => new TestModel { Value = "updated" });

            var updatedContent = File.ReadAllText(filePath);
            Assert.IsTrue(updatedContent.Contains("/* User settings*/"));
            Assert.IsTrue(updatedContent.Contains("/* Keep this structure */"));
        }

        [TestMethod]
        public async Task TestConcurrentReads()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var entity = new TestObservableJsonEntity(filePath, () => new TestModel { Value = "initial" });

            var tasks = new List<Task<TestModel>>();

            for (int i = 0; i < 5; i++)
            {
                tasks.Add(entity.Value.FirstAsync().ToTask());
            }

            var results = await Task.WhenAll(tasks);

            Assert.AreEqual(5, results.Length);
            foreach (var value in results)
            {
                Assert.AreEqual("initial", value.Value);
            }
        }

        [TestMethod]
        public async Task TestSequentialSaves()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var entity = new TestObservableJsonEntity(filePath, () => new TestModel { Value = "initial" });

            await entity.SaveAsync(x => new TestModel { Value = "save1" });
            await entity.SaveAsync(x => new TestModel { Value = "save2" });
            await entity.SaveAsync(x => new TestModel { Value = "save3" });

            var finalContent = File.ReadAllText(filePath);
            Assert.IsTrue(finalContent.Contains("save3"));
        }

        [TestMethod]
        public async Task TestMergePreservesStructure()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var originalJson = @"{
                ""Value"": ""original""
            }";
            File.WriteAllText(filePath, originalJson);

            var entity = new TestObservableJsonEntityWithComments(filePath, () => new TestModel { Value = "default" });

            await entity.SaveAsync(x => new TestModel { Value = "updated" });

            using var doc = JsonDocument.Parse(File.ReadAllText(filePath));
            Assert.IsTrue(doc.RootElement.TryGetProperty("Value", out _));
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public async Task TestThrowsOnUnauthorizedAccess()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var entity = new TestObservableJsonEntity(filePath, () => new TestModel { Value = "initial" });

            await entity.SaveAsync(TestModel.Next);

            var fileInfo = new FileInfo(filePath);
            var originalAttributes = fileInfo.Attributes;

            try
            {
                fileInfo.Attributes |= FileAttributes.ReadOnly;

                await entity.SaveAsync(x => new TestModel { Value = "should_fail" });
            }
            finally
            {
                fileInfo.Attributes = originalAttributes;
            }
        }

        [TestMethod]
        public async Task TestNestedObjectMerge()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var originalJson = @"{
                // Settings
                ""Nested"": {
                    ""Value"": ""original""
                }
            }";
            File.WriteAllText(filePath, originalJson);

            var entity = new TestObservableJsonEntityWithComments(filePath, () => new TestModel { Value = "default" });

            await entity.SaveAsync(x => new TestModel { Value = "updated" });

            var updatedContent = File.ReadAllText(filePath);
            Assert.IsTrue(updatedContent.Contains("/* Settings*/"));
            Assert.IsTrue(updatedContent.Contains("updated"));
        }

        [TestMethod]
        public async Task TestAddNewPropertiesPreservingComments()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var originalJson = @"{
                // Original property
                ""Value"": ""original""
            }";
            File.WriteAllText(filePath, originalJson);

            var entity = new TestObservableJsonEntityWithComments(filePath, () => new TestModel { Value = "default" });

            await entity.SaveAsync(TestModel.Next);

            var updatedContent = File.ReadAllText(filePath);
            Assert.IsTrue(updatedContent.Contains("/* Original property*/"));
            Assert.IsTrue(updatedContent.Contains("original"));
        }

        [TestMethod]
        public async Task TestArrayPreservation()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var originalJson = @"{
                // Items array
                ""Items"": [
                    // First item
                    ""one"",
                    ""two""
                ]
            }";
            File.WriteAllText(filePath, originalJson);

            var entity = new TestObservableJsonEntityWithComments(filePath, () => new TestModel { Value = "default" });

            await entity.SaveAsync(TestModel.Next);

            var content = File.ReadAllText(filePath);
            Assert.IsTrue(content.Contains("/* Items array*/"));
            Assert.IsTrue(content.Contains("/* First item*/"));
            Assert.IsTrue(content.Contains("\"one\""));
            Assert.IsTrue(content.Contains("\"two\""));
        }

        [TestMethod]
        public async Task TestCommentBetweenProperties()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var originalJson = @"{
                ""First"": ""value1"",
                // Comment between properties
                ""Second"": ""value2""
            }";
            File.WriteAllText(filePath, originalJson);

            var entity = new TestObservableJsonEntityWithComments(filePath, () => new TestModel { Value = "default" });

            await entity.SaveAsync(TestModel.Next);

            var content = File.ReadAllText(filePath);
            Assert.IsTrue(content.Contains("/* Comment between properties*/"));
        }

        [TestMethod]
        public async Task TestBlockCommentPreservation()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var originalJson = @"{
                /* Multi-line
                   block comment */
                ""Value"": ""original""
            }";
            File.WriteAllText(filePath, originalJson);

            var entity = new TestObservableJsonEntityWithComments(filePath, () => new TestModel { Value = "default" });
            
            await entity.SaveAsync(TestModel.Next);

            var content = File.ReadAllText(filePath);
            Assert.IsTrue(content.Contains("/* Multi-line"));
            Assert.IsTrue(content.Contains("block comment */"));
        }

        [TestMethod]
        public async Task TestUpdateValueWithSurroundingComments()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var originalJson = @"{
                // Before property
                ""Value"": ""original"",
                // After property
                ""Other"": 123
            }";
            File.WriteAllText(filePath, originalJson);

            var entity = new TestObservableJsonEntityWithComments(filePath, () => new TestModel { Value = "default" });

            await entity.SaveAsync(x => new TestModel { Value = "updated" });

            var updatedContent = File.ReadAllText(filePath);
            Assert.IsTrue(updatedContent.Contains("/* Before property*/"));
            Assert.IsTrue(updatedContent.Contains("/* After property*/"));
            Assert.IsTrue(updatedContent.Contains("updated"));
        }

        [TestMethod]
        public async Task TestComplexNestedStructureWithAllCommentTypes()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var originalJson = @"{
                // Root level
                ""Config"": {
                    // Nested config
                    ""Value"": ""original"", // inline
                    /* Block
                       comment */
                    ""Items"": [
                        // First
                        ""one"",
                        // Second
                        ""two""
                    ]
                }
            }";
            File.WriteAllText(filePath, originalJson);

            var entity = new TestObservableJsonEntityWithComments(filePath, () => new TestModel { Value = "default" });

            await entity.SaveAsync(TestModel.Next);

            var content = File.ReadAllText(filePath);
            Assert.IsTrue(content.Contains("/* Root level*/"));
            Assert.IsTrue(content.Contains("/* Nested config*/"));
            Assert.IsTrue(content.Contains("/* inline*/"));
            Assert.IsTrue(content.Contains("/* Block"));
            Assert.IsTrue(content.Contains("/* First*/"));
            Assert.IsTrue(content.Contains("/* Second*/"));
            Assert.IsTrue(content.Contains("\"one\""));
            Assert.IsTrue(content.Contains("\"two\""));
        }

        [TestMethod]
        public async Task TestBooleanPropertiesWithComments()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var originalJson = @"{
                // Feature flag
                ""Enabled"": true,
                // Disabled flag
                ""Disabled"": false
            }";
            File.WriteAllText(filePath, originalJson);

            var entity = new TestObservableJsonEntityWithComments(filePath, () => new TestModel { Value = "default" });

            await entity.SaveAsync(TestModel.Next);

            var content = File.ReadAllText(filePath);
            Assert.IsTrue(content.Contains("/* Feature flag*/"));
            Assert.IsTrue(content.Contains("true"));
            Assert.IsTrue(content.Contains("/* Disabled flag*/"));
            Assert.IsTrue(content.Contains("false"));
        }

        [TestMethod]
        public async Task TestCommentAfterLastProperty()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var originalJson = @"{
                ""Value"": ""original""
                // Final comment
            }";
            File.WriteAllText(filePath, originalJson);

            var entity = new TestObservableJsonEntityWithComments(filePath, () => new TestModel { Value = "default" });

            await entity.SaveAsync(TestModel.Next);

            var content = File.ReadAllText(filePath);
            Assert.IsTrue(content.Contains("/* Final comment*/"));
        }

        [TestMethod]
        public async Task TestEmptyObjectWithComments()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var originalJson = @"{
                // Empty nested object
                ""Empty"": {}
            }";
            File.WriteAllText(filePath, originalJson);

            var entity = new TestObservableJsonEntityWithComments(filePath, () => new TestModel { Value = "default" });

            await entity.SaveAsync(TestModel.Next);

            var content = File.ReadAllText(filePath);
            Assert.IsTrue(content.Contains("/* Empty nested object*/"));
            Assert.IsTrue(content.Contains("{}"));
        }

        [TestMethod]
        public async Task TestEmptyArrayWithComments()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var originalJson = @"{
                // Empty array
                ""Items"": []
            }";
            File.WriteAllText(filePath, originalJson);

            var entity = new TestObservableJsonEntityWithComments(filePath, () => new TestModel { Value = "default" });

            await entity.SaveAsync(TestModel.Next);

            var content = File.ReadAllText(filePath);
            Assert.IsTrue(content.Contains("/* Empty array*/"));
            Assert.IsTrue(content.Contains("[]"));
        }

        ObservableJsonEntityWithCommentPreservation<T> Make<T>(string fullPath, T entity) where T : class
        {
            return new ObservableJsonEntityWithCommentPreservation<T>(fullPath, () => entity);
        }

        [TestMethod]
        public async Task TestDeepMergeOrder()
        {
            var filePath = Path.Combine(m_tempDir, "test.json");
            var originalJson = @"{
                ""root"": {
                    ""object"": {
                        ""foo"": ""a"",
                        ""bar"": ""b"",
                        ""baz"": ""c""
                    },
                    ""array"": [1, 2, 3],
                    ""booolean"": true,
                    ""number"": 1,
                    ""null"": null
                }
            }";
            File.WriteAllText(filePath, originalJson);

            var replacement = new
            {
                root = new
                {
                    @object = new
                    {
                        a = "foo",
                        b = "bar",
                        c = "baz",
                        foo = "bar",
                        baz = "foo",
                    },
                    array = new[] { 3, 2, 1 },
                    boolean = true,
                    number = 2,
                    @null = new { },
                }
            };

            var entity = Make(filePath, replacement);

            await entity.SaveAsync(_ => replacement);

            var content = File.ReadAllText(filePath);
            Assert.AreEqual(content, """{"root":{"object":{"foo":"bar","bar":"b","baz":"foo","a":"foo","b":"bar","c":"baz"},"array":[3,2,1],"boolean":true,"number":2,"null":{}}}""");
        }
    }

    public class TestModel
    {
        public string Value { get; set; } = null!;

        public int Counter { get; set; }

        public static TestModel Next(TestModel input)
        {
            return new TestModel { Value = input.Value, Counter = input.Counter + 1 };
        }
    }

    public class TestObservableJsonEntity : ObservableJsonEntity<TestModel>
    {
        public TestObservableJsonEntity(string fullPath, Func<TestModel> defaultFactory)
            : base(fullPath, defaultFactory, new JsonSerializerOptions { WriteIndented = true })
        {
        }
    }

    public class TestObservableJsonEntityWithComments : ObservableJsonEntityWithCommentPreservation<TestModel>
    {
        public TestObservableJsonEntityWithComments(string fullPath, Func<TestModel> defaultFactory)
            : base(fullPath, defaultFactory, new JsonSerializerOptions { WriteIndented = true })
        {
        }
    }
}
