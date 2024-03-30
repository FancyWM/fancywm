
using System;
using System.Diagnostics;

using FancyWM.Utilities;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyWM.Tests.Utilities
{
    [TestClass]
    public class ErrorEncoderTest
    {
        [TestMethod]
        public void TestErrorCodeUnknown()
        {
            Assert.AreEqual("EUNK", ErrorEncoder.GetErrorCodeString(new Exception()));
        }

        [TestMethod]
        public void TestErrorCodeKnown()
        {
            try
            {
                throw new ArgumentException();
            }
            catch (Exception e)
            {
                Assert.AreEqual($"EET/EET/TECK/{GetSourceLine(e)}", ErrorEncoder.GetErrorCodeString(e));
            }
        }

        private int GetSourceLine(Exception e)
        {
            return (new StackTrace(e.GetBaseException(), true)).GetFrame(0).GetFileLineNumber();
        }
    }
}
