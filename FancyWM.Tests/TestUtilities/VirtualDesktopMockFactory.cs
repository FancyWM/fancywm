using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Moq;

using WinMan;

namespace FancyWM.Tests.TestUtilities
{
    internal class VirtualDesktopMockFactory
    {
        public IVirtualDesktop CreateVirtualDesktop()
        {
            var mock = new Mock<IVirtualDesktop>();
            mock.SetupGet(x => x.Index).Returns(0);
            mock.SetupGet(x => x.Name).Returns("Desktop 1");
            mock.SetupGet(x => x.IsCurrent).Returns(true);
            mock.Setup(x => x.HasWindow(It.IsAny<IWindow>())).Returns(true);
            return mock.Object;
        }
    }
}
