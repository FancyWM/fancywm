
using System;
using System.Threading.Tasks;
using System.Windows.Threading;

//using FancyWM.Tests.TestUtilities;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyWM.Utilities.Tests
{
    [TestClass]
    public class CountdownTimerTest
    {
        [TestMethod]
        public void TestSimple()
        {
            var timer = new CountdownTimer();
            Assert.IsTrue(timer.IsDone);
        }

        //[TestMethod]
        //public async Task TestWaitAsync()
        //{
        //    await Task.Factory.StartNew(async () =>
        //    {
        //        Dispatcher.Run();
        //        var timer = new CountdownTimer();
        //        Assert.IsTrue(timer.IsDone);
        //        var t = timer.SetRemainingAsync(TimeSpan.FromMilliseconds(5));
        //        Assert.IsFalse(timer.IsDone);
        //        while (!t.IsCompleted)
        //        {
        //            Dispatchers.DoEvents();
        //        }
        //        await t;
        //        Assert.IsTrue(timer.IsDone);
        //    });
        //}
    }
}
