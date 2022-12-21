using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;

using FancyWM.Converters;
using FancyWM.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyWM.Tests.Converters
{
    [TestClass]
    public class LegacyKeybindingConverterTest
    {
        [TestMethod]
        public void TestParseEmpty()
        {
            Assert.AreEqual(WriteString(ReadString("{}")), WriteString(new LegacyKeybindingDictionary()));
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
            }")), WriteString(new LegacyKeybindingDictionary()
            {
                { BindableAction.MoveFocusDown, new LegacyKeybinding(new[] { Key.Down }.ToHashSet(), isDirectMode: false) },
                { BindableAction.SwapLeft, new LegacyKeybinding(new[] { Key.LeftShift, Key.Left }.ToHashSet(), isDirectMode: false) },
            }));
        }


        [TestMethod]
        public void TestWriteEmpty()
        {
            var testObj = new LegacyKeybindingDictionary()
            {
                { BindableAction.MoveFocusDown, new LegacyKeybinding(new[] { Key.Down }.ToHashSet(), isDirectMode: false) },
                { BindableAction.SwapLeft, new LegacyKeybinding(new[] { Key.LeftShift, Key.Left }.ToHashSet(), isDirectMode: true) },
            };
            Assert.AreEqual(WriteString(ReadString(WriteString(testObj))), WriteString(testObj));
        }

        private static LegacyKeybindingDictionary ReadString(string s)
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(s));
            reader.Read();
            return new LegacyKeybindingConverter(useDefaults: false).Read(ref reader, typeof(string), new JsonSerializerOptions());
        }

        private static string WriteString(LegacyKeybindingDictionary keybindings)
        {
            using (MemoryStream stream = new())
            {
                var writer = new Utf8JsonWriter(stream);
                new LegacyKeybindingConverter(useDefaults: false).Write(writer, keybindings, new JsonSerializerOptions());
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
