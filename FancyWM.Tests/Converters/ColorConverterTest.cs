using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Media;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ColorConverter = FancyWM.Converters.ColorConverter;

namespace FancyWM.Tests.Converters
{
    [TestClass]
    public class ColorConverterTest
    {
        [TestMethod]
        public void TestConvertRgbLowerRange()
        {
            Assert.AreEqual(Color.FromRgb(0x12, 0x34, 0x56), ReadString("#123456"));
        }

        [TestMethod]
        public void TestConvertRgbUpperRange()
        {
            Assert.AreEqual(Color.FromRgb(0xAB, 0xCD, 0xEF), ReadString("#ABCDEF"));
        }

        [TestMethod]
        public void TestConvertRgbUpperRangeLowercase()
        {
            Assert.AreEqual(Color.FromRgb(0xAB, 0xCD, 0xEF), ReadString("#abcdef"));
        }

        [TestMethod]
        public void TestConvertRgbaLowerRange()
        {
            Assert.AreEqual(Color.FromArgb(0x44, 0x11, 0x22, 0x33), ReadString("#11223344"));
        }

        [TestMethod]
        public void TestConvertRgbaUpperRange()
        {
            Assert.AreEqual(Color.FromArgb(0xDD, 0xAA, 0xBB, 0xCC), ReadString("#AABBCCDD"));
        }

        [TestMethod]
        public void TestConvertRgbaUpperRangeLowercase()
        {
            Assert.AreEqual(Color.FromArgb(0xDD, 0xAA, 0xBB, 0xCC), ReadString("#aabbccdd"));
        }

        [TestMethod]
        public void TestFailsOnTooLongInput()
        {
            Assert.ThrowsException<FormatException>(() => ReadString("#aabbccddeeff"));
        }

        [TestMethod]
        public void TestFailsOnTooShortInput()
        {
            Assert.ThrowsException<FormatException>(() => ReadString("#abc"));
        }

        [TestMethod]
        public void TestFailsOnWrongInputLength()
        {
            Assert.ThrowsException<FormatException>(() => ReadString("#abcdef0"));
        }

        [TestMethod]
        public void TestFailsWithoutHash()
        {
            Assert.ThrowsException<FormatException>(() => ReadString("C0C0C0"));
        }

        [TestMethod]
        public void TestWriteRgb()
        {
            Assert.AreEqual("#123456FF", WriteString(Color.FromRgb(0x12, 0x34, 0x56)));
        }

        [TestMethod]
        public void TestWriteRgba()
        {
            Assert.AreEqual("#123456AB", WriteString(Color.FromArgb(0xAB, 0x12, 0x34, 0x56)));
        }

        [TestMethod]
        public void TestFailsOnInvalidInput()
        {
            Assert.ThrowsException<FormatException>(() => ReadString("!@#N#ID*"));
        }

        [TestMethod]
        public void TestFailsOnInvalidInputWithHash()
        {
            Assert.ThrowsException<FormatException>(() => ReadString("#!@#N#ID*"));
        }

        private static Color ReadString(string s)
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes($"\"{s}\""));
            reader.Read();
            return new ColorConverter().Read(ref reader, typeof(string), new JsonSerializerOptions());
        }

        private static string WriteString(Color color)
        {
            using MemoryStream stream = new();
            var writer = new Utf8JsonWriter(stream);
            new ColorConverter().Write(writer, color, new JsonSerializerOptions());
            writer.Flush();
            stream.Position = 0;
            using StreamReader reader = new(stream);
            return reader.ReadToEnd().Trim('"');
        }
    }
}
