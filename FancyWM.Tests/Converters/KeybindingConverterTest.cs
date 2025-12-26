using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using FancyWM.Converters;
using FancyWM.Models;
using FancyWM.Utilities;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyWM.Tests.Converters
{
    [TestClass]
    public class KeybindingConverterTest
    {
        [TestMethod]
        public void TestParseEmpty()
        {
            Assert.AreEqual(WriteString(ReadString("{}")), WriteString(new KeybindingDictionary(useDefaults: false)));
        }

        [TestMethod]
        public void TestParse()
        {
            Assert.AreEqual(WriteString(ReadString(@"{
                ""MoveFocusDown"": {
                  ""IsDirectMode"": false,
                  ""Keys"": [
                    ""Down""
                  ]
                },
                ""SwapLeft"": {
                  ""IsDirectMode"": false,
                  ""Keys"": [
                    ""LeftShift"",
                    ""Left""
                  ]
                }
            }")), WriteString(new KeybindingDictionary(useDefaults: false)
            {
                { BindableAction.MoveFocusDown, new Keybinding(new[] { KeyCode.Down }.ToHashSet(), isDirectMode: false) },
                { BindableAction.SwapLeft, new Keybinding(new[] { KeyCode.LeftShift, KeyCode.Left }.ToHashSet(), isDirectMode: false) },
            }));
        }

        [TestMethod]
        public void TestParseDirectMode()
        {
            Assert.AreEqual(WriteString(ReadString(@"{
                ""ToggleManager"": {
                  ""IsDirectMode"": true,
                  ""Keys"": [
                    ""F11""
                  ]
                }
            }")), WriteString(new KeybindingDictionary(useDefaults: false)
            {
                { BindableAction.ToggleManager, new Keybinding(new[] { KeyCode.F11 }.ToHashSet(), isDirectMode: true) },
            }));
        }

        [TestMethod]
        public void TestParseNullKeybinding()
        {
            var result = ReadString(@"{
                ""MoveFocusDown"": null
            }");

            Assert.IsNull(result[BindableAction.MoveFocusDown]);
        }

        [TestMethod]
        public void TestParseMultipleKeyCombinations()
        {
            var result = ReadString(@"{
                ""SwapLeft"": {
                  ""IsDirectMode"": false,
                  ""Keys"": [
                    ""LeftCtrl"",
                    ""LeftAlt"",
                    ""Left""
                  ]
                }
            }");

            var keybinding = result[BindableAction.SwapLeft];
            Assert.IsNotNull(keybinding);
            Assert.AreEqual(3, keybinding.Keys.Count);
            Assert.IsTrue(keybinding.Keys.Contains(KeyCode.LeftCtrl));
            Assert.IsTrue(keybinding.Keys.Contains(KeyCode.LeftAlt));
            Assert.IsTrue(keybinding.Keys.Contains(KeyCode.Left));
        }

        [TestMethod]
        public void TestWriteEmpty()
        {
            var testObj = new KeybindingDictionary(useDefaults: false)
            {
                { BindableAction.MoveFocusDown, new Keybinding(new[] { KeyCode.Down }.ToHashSet(), isDirectMode: false) },
                { BindableAction.SwapLeft, new Keybinding(new[] { KeyCode.LeftShift, KeyCode.Left }.ToHashSet(), isDirectMode: true) },
            };
            Assert.AreEqual(WriteString(ReadString(WriteString(testObj))), WriteString(testObj));
        }

        [TestMethod]
        public void TestRoundTripSingleKey()
        {
            var original = new KeybindingDictionary(useDefaults: false)
            {
                { BindableAction.ToggleManager, new Keybinding(new[] { KeyCode.F11 }.ToHashSet(), isDirectMode: false) },
            };

            var json = WriteString(original);
            var deserialized = ReadString(json);
            var reserialized = WriteString(deserialized);

            Assert.AreEqual(json, reserialized);
        }

        [TestMethod]
        public void TestRoundTripMultipleKeybindings()
        {
            var original = new KeybindingDictionary(useDefaults: false)
            {
                { BindableAction.MoveFocusDown, new Keybinding(new[] { KeyCode.Down }.ToHashSet(), isDirectMode: false) },
                { BindableAction.MoveFocusUp, new Keybinding(new[] { KeyCode.Up }.ToHashSet(), isDirectMode: false) },
                { BindableAction.SwapLeft, new Keybinding(new[] { KeyCode.LeftShift, KeyCode.Left }.ToHashSet(), isDirectMode: true) },
                { BindableAction.ToggleManager, new Keybinding(new[] { KeyCode.F11 }.ToHashSet(), isDirectMode: false) },
            };

            var json = WriteString(original);
            var deserialized = ReadString(json);
            var reserialized = WriteString(deserialized);

            Assert.AreEqual(json, reserialized);
        }

        [TestMethod]
        public void TestRoundTripWithNullKeybinding()
        {
            var original = new KeybindingDictionary(useDefaults: false)
            {
                { BindableAction.MoveFocusDown, new Keybinding(new[] { KeyCode.Down }.ToHashSet(), isDirectMode: false) },
                { BindableAction.SwapLeft, null },
            };

            var json = WriteString(original);
            var deserialized = ReadString(json);

            Assert.IsNull(deserialized[BindableAction.SwapLeft]);
            Assert.AreEqual(WriteString(original), WriteString(deserialized));
        }

        [TestMethod]
        public void TestBackwardCompatibility2dot3dot5()
        {
            var legacyJson = @"{
                ""MoveFocusDown"": [""Down""],
                ""SwapLeft"": [""LeftShift"", ""Left""]
            }";

            var result = ReadString(legacyJson);
            Assert.IsNotNull(result[BindableAction.MoveFocusDown]);
            Assert.IsNotNull(result[BindableAction.SwapLeft]);

            Assert.AreEqual(1, result[BindableAction.MoveFocusDown].Keys.Count);
            Assert.IsTrue(result[BindableAction.MoveFocusDown].Keys.Contains(KeyCode.Down));
            Assert.IsFalse(result[BindableAction.MoveFocusDown].IsDirectMode);

            Assert.AreEqual(2, result[BindableAction.SwapLeft].Keys.Count);
            Assert.IsTrue(result[BindableAction.SwapLeft].Keys.Contains(KeyCode.LeftShift));
            Assert.IsTrue(result[BindableAction.SwapLeft].Keys.Contains(KeyCode.Left));
            Assert.IsFalse(result[BindableAction.SwapLeft].IsDirectMode);
        }

        [TestMethod]
        public void TestInvalidKeyCodeIgnored()
        {
            var json = @"{
                ""MoveFocusDown"": {
                    ""IsDirectMode"": false,
                    ""Keys"": [""Down"", ""InvalidKey""]
                }
            }";

            var result = ReadString(json);
            Assert.AreEqual(0, result.Keys.Count);
        }

        [TestMethod]
        public void TestCaseInsensitiveActionParsing()
        {
            var json = @"{
                ""movefocusdown"": {
                    ""IsDirectMode"": false,
                    ""Keys"": [""Down""]
                }
            }";

            var result = ReadString(json);
            Assert.IsNotNull(result[BindableAction.MoveFocusDown]);
        }

        [TestMethod]
        public void TestCaseInsensitiveKeyCodeParsing()
        {
            var json = @"{
                ""MoveFocusDown"": {
                    ""IsDirectMode"": false,
                    ""Keys"": [""down""]
                }
            }";

            var result = ReadString(json);
            Assert.IsNotNull(result[BindableAction.MoveFocusDown]);
            Assert.IsTrue(result[BindableAction.MoveFocusDown].Keys.Contains(KeyCode.Down));
        }

        [TestMethod]
        public void TestIsDirectModePreserved()
        {
            var originalFalse = new KeybindingDictionary(useDefaults: false)
            {
                { BindableAction.ToggleManager, new Keybinding(new[] { KeyCode.F11 }.ToHashSet(), isDirectMode: false) },
            };

            var originalTrue = new KeybindingDictionary(useDefaults: false)
            {
                { BindableAction.ToggleManager, new Keybinding(new[] { KeyCode.F11 }.ToHashSet(), isDirectMode: true) },
            };

            var jsonFalse = WriteString(originalFalse);
            var jsonTrue = WriteString(originalTrue);

            var deserializedFalse = ReadString(jsonFalse);
            var deserializedTrue = ReadString(jsonTrue);

            Assert.IsFalse(deserializedFalse[BindableAction.ToggleManager].IsDirectMode);
            Assert.IsTrue(deserializedTrue[BindableAction.ToggleManager].IsDirectMode);
        }

        [TestMethod]
        public void TestAllKeysPreserved()
        {
            var keys = new[] { KeyCode.LeftCtrl, KeyCode.LeftAlt, KeyCode.LeftShift, KeyCode.Down };
            var original = new KeybindingDictionary(useDefaults: false)
            {
                { BindableAction.MoveFocusDown, new Keybinding(keys.ToHashSet(), isDirectMode: false) },
            };

            var json = WriteString(original);
            var deserialized = ReadString(json);

            Assert.AreEqual(keys.Length, deserialized[BindableAction.MoveFocusDown].Keys.Count);
            foreach (var key in keys)
            {
                Assert.IsTrue(deserialized[BindableAction.MoveFocusDown].Keys.Contains(key));
            }
        }

        [TestMethod]
        public void TestNull()
        {
            var original = new KeybindingDictionary(useDefaults: false)
            {
                { BindableAction.MoveFocusDown, null },
            };

            var json = WriteString(original);
            var deserialized = ReadString(json);

            Assert.IsNull(deserialized[BindableAction.MoveFocusDown]);
        }

        [TestMethod]
        public void TestUseDefaultsTrue()
        {
            var converter = new KeybindingConverter(useDefaults: true);
            var result = new KeybindingDictionary(useDefaults: true);

            Assert.IsTrue(result.Count > 0);
        }

        [TestMethod]
        public void TestUseDefaultsFalse()
        {
            var converter = new KeybindingConverter(useDefaults: false);
            var result = new KeybindingDictionary(useDefaults: false);

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void TestMalformedJsonThrows()
        {
            var malformedJson = "{invalid json}";

            Assert.ThrowsException<AggregateException>(() => ReadString(malformedJson));
        }

        [TestMethod]
        public void TestMultipleActionsWithMixedDirectMode()
        {
            var original = new KeybindingDictionary(useDefaults: false)
            {
                { BindableAction.MoveFocusDown, new Keybinding(new[] { KeyCode.Down }.ToHashSet(), isDirectMode: false) },
                { BindableAction.ToggleManager, new Keybinding(new[] { KeyCode.F11 }.ToHashSet(), isDirectMode: true) },
                { BindableAction.RefreshWorkspace, new Keybinding(new[] { KeyCode.R }.ToHashSet(), isDirectMode: false) },
                { BindableAction.SwapLeft, new Keybinding(new[] { KeyCode.LeftShift, KeyCode.Left }.ToHashSet(), isDirectMode: true) },
            };

            var json = WriteString(original);
            var deserialized = ReadString(json);

            Assert.IsFalse(deserialized[BindableAction.MoveFocusDown].IsDirectMode);
            Assert.IsTrue(deserialized[BindableAction.ToggleManager].IsDirectMode);
            Assert.IsFalse(deserialized[BindableAction.RefreshWorkspace].IsDirectMode);
            Assert.IsTrue(deserialized[BindableAction.SwapLeft].IsDirectMode);
        }

        [TestMethod]
        public void TestJsonStructureCorrect()
        {
            var original = new KeybindingDictionary(useDefaults: false)
            {
                { BindableAction.ToggleManager, new Keybinding(new[] { KeyCode.F11 }.ToHashSet(), isDirectMode: true) },
                { BindableAction.CreateHorizontalPanel, new Keybinding(new[] { KeyCode.H, KeyCode.A }.ToHashSet(), isDirectMode: false) },
            };

            var json = WriteString(original);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.IsTrue(root.TryGetProperty("ToggleManager", out var toggleManager));
            Assert.AreEqual(toggleManager.GetString(), "F11");

            Assert.IsTrue(root.TryGetProperty("CreateHorizontalPanel", out var createPanel));
            Assert.AreEqual(createPanel.GetString(), "Activation H+A");
        }

        [TestMethod]
        public void TestLegacyJsonStructureCorrect()
        {
            var legacyJson = @"{
                ""MoveFocusDown"": [""Down""]
            }";

            using var doc = JsonDocument.Parse(legacyJson);
            var root = doc.RootElement;

            Assert.IsTrue(root.TryGetProperty("MoveFocusDown", out var moveFocus));
            Assert.AreEqual(JsonValueKind.Array, moveFocus.ValueKind);
        }

        [TestMethod]
        public void TestComplexBackwardCompatibilityScenario()
        {
            var legacyJson = @"{
                ""MoveFocusDown"": [""Down""],
                ""MoveFocusUp"": [""Up""],
                ""SwapLeft"": [""LeftShift"", ""Left""],
                ""SwapRight"": [""RightShift"", ""Right""]
            }";

            var result = ReadString(legacyJson);

            Assert.AreEqual(4, result.Count);
            Assert.IsTrue(result.All(kvp => !kvp.Value.IsDirectMode));
            Assert.IsTrue(result.All(kvp => kvp.Value.Keys.Count > 0));
        }

        [TestMethod]
        public void TestKeyOrderPreservedInRoundTrip()
        {
            var original = new KeybindingDictionary(useDefaults: false)
            {
                { BindableAction.MoveFocusDown, new Keybinding(new[] { KeyCode.Down }.ToHashSet(), isDirectMode: false) },
                { BindableAction.MoveFocusUp, new Keybinding(new[] { KeyCode.Up }.ToHashSet(), isDirectMode: false) },
            };

            var json1 = WriteString(original);
            var deserialized = ReadString(json1);
            var json2 = WriteString(deserialized);

            Assert.AreEqual(json1, json2);
        }

        [TestMethod]
        public void TestNullValueInDictionary()
        {
            var original = new KeybindingDictionary(useDefaults: false)
            {
                { BindableAction.MoveFocusDown, null },
                { BindableAction.SwapLeft, new Keybinding(new[] { KeyCode.Left }.ToHashSet(), isDirectMode: false) },
            };

            var json = WriteString(original);
            var deserialized = ReadString(json);

            Assert.IsNull(deserialized[BindableAction.MoveFocusDown]);
            Assert.IsNotNull(deserialized[BindableAction.SwapLeft]);
        }

        [TestMethod]
        public void TestUnknownEnumValueInLegacyFormat()
        {
            var legacyJson = @"{
                ""UnknownAction"": [""Down""]
            }";

            var result = ReadString(legacyJson);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void TestPartiallyValidLegacyJson()
        {
            var legacyJson = @"{
                ""MoveFocusDown"": [""Down""],
                ""UnknownAction"": [""InvalidKey""],
                ""SwapLeft"": [""LeftShift"", ""Left""]
            }";

            var result = ReadString(legacyJson);
            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.ContainsKey(BindableAction.MoveFocusDown));
            Assert.IsTrue(result.ContainsKey(BindableAction.SwapLeft));
        }

        [TestMethod]
        public void TestSpecialCharacterInKeysArray()
        {
            var json = @"{
                ""MoveFocusDown"": {
                    ""IsDirectMode"": false,
                    ""Keys"": [""OemMinus"", ""OemPlus"", ""Down""]
                }
            }";

            var result = ReadString(json);
            Assert.IsNotNull(result[BindableAction.MoveFocusDown]);
            Assert.AreEqual(3, result[BindableAction.MoveFocusDown].Keys.Count);
        }

        [TestMethod]
        public void TestParseEmptyKeysArray()
        {
            var json = @"{
                ""MoveFocusDown"": {
                    ""IsDirectMode"": false,
                    ""Keys"": []
                }
            }";

            var result = ReadString(json);
            Assert.IsNotNull(result[BindableAction.MoveFocusDown]);
            Assert.AreEqual(0, result[BindableAction.MoveFocusDown].Keys.Count);
        }

        [TestMethod]
        public void TestModifierKeysOnlyNoActionKey()
        {
            var original = new KeybindingDictionary(useDefaults: false)
            {
                { BindableAction.ToggleManager, new Keybinding(new[] { KeyCode.LeftCtrl, KeyCode.LeftAlt }.ToHashSet(), isDirectMode: false) },
            };

            var json = WriteString(original);
            var deserialized = ReadString(json);

            Assert.AreEqual(2, deserialized[BindableAction.ToggleManager].Keys.Count);
        }

        private static KeybindingDictionary ReadString(string s)
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(s));
            reader.Read();
            return new KeybindingConverter(useDefaults: false).Read(ref reader, typeof(KeybindingDictionary), new JsonSerializerOptions());
        }

        private static string WriteString(KeybindingDictionary keybindings)
        {
            using MemoryStream stream = new();
            var writer = new Utf8JsonWriter(stream);
            new KeybindingConverter(useDefaults: false).Write(writer, keybindings, new JsonSerializerOptions());
            writer.Flush();
            stream.Position = 0;
            using StreamReader reader = new(stream);
            return reader.ReadToEnd();
        }
    }
}
