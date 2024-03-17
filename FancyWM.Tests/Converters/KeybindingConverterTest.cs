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
                ""MoveFocusDown"": [
                  ""Down""
                ],
                ""SwapLeft"": [
                  ""LeftShift"",
                  ""Left""
                ]
            }")), WriteString(new KeybindingDictionary(useDefaults: false)
            {
                { BindableAction.MoveFocusDown, new Keybinding(new[] { KeyCode.Down }.ToHashSet(), isDirectMode: false) },
                { BindableAction.SwapLeft, new Keybinding(new[] { KeyCode.LeftShift, KeyCode.Left }.ToHashSet(), isDirectMode: false) },
            }));
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

        private static KeybindingDictionary ReadString(string s)
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(s));
            reader.Read();
            return new KeybindingConverter(useDefaults: false).Read(ref reader, typeof(string), new JsonSerializerOptions());
        }

        private static string WriteString(KeybindingDictionary keybindings)
        {
            using (MemoryStream stream = new())
            {
                var writer = new Utf8JsonWriter(stream);
                new KeybindingConverter(useDefaults: false).Write(writer, keybindings, new JsonSerializerOptions());
                writer.Flush();
                stream.Position = 0;
                using (StreamReader reader = new(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
