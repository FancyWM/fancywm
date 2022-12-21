using System;
using System.Diagnostics;

using Moq;

using WinMan;

namespace FancyWM.Tests.TestUtilities
{
    internal class WindowMockFactory
    {
        public IWindow CreateDiscordWindow()
        {
            var mock = CreateBaseMock();
            mock.SetupGet(x => x.Title).Returns("Discord");
            mock.SetupGet(x => x.MinSize).Returns(new Point(940, 500));
            mock.SetupGet(x => x.MaxSize).Returns((Point?)null);
            return mock.Object;
        }

        public IWindow CreateExplorerWindow()
        {
            var mock = CreateBaseMock();
            mock.SetupGet(x => x.Title).Returns("This PC");
            mock.SetupGet(x => x.MinSize).Returns(new Point(161, 243));
            mock.SetupGet(x => x.MaxSize).Returns((Point?)null);
            mock.Setup(x => x.GetProcess()).Returns(Process.GetProcessesByName("explorer")[0]);
            return mock.Object;
        }

        public IWindow CreateNotepadWindow()
        {
            var mock = CreateBaseMock();
            mock.SetupGet(x => x.Title).Returns("Untitled - Notepad");
            mock.SetupGet(x => x.MinSize).Returns((Point?)null);
            mock.SetupGet(x => x.MaxSize).Returns((Point?)null);
            return mock.Object;
        }

        private Mock<IWindow> CreateBaseMock()
        {
            var mock = new Mock<IWindow>();
            var hash = mock.GetHashCode();
            mock.SetupGet(x => x.Handle).Returns(new IntPtr(10));
            mock.SetupGet(x => x.CanClose).Returns(true);
            mock.SetupGet(x => x.CanMaximize).Returns(true);
            mock.SetupGet(x => x.CanMinimize).Returns(true);
            mock.SetupGet(x => x.CanMove).Returns(true);
            mock.SetupGet(x => x.CanReorder).Returns(true);
            mock.SetupGet(x => x.CanResize).Returns(true);
            mock.SetupGet(x => x.FrameMargins).Returns(new Rectangle());
            mock.SetupGet(x => x.Handle).Throws(new NotSupportedException());
            mock.SetupGet(x => x.IsAlive).Returns(true);
            mock.SetupGet(x => x.IsFocused).Returns(false);
            mock.SetupGet(x => x.IsTopmost).Returns(false);
            mock.SetupGet(x => x.Position).Returns(() => new Rectangle(0, 0, 1024, 768));
            mock.SetupGet(x => x.State).Returns(WindowState.Restored);
            mock.Setup(x => x.GetHashCode()).Returns(hash);
            mock.Setup(x => x.Equals(It.IsAny<object>())).Returns(false);
            mock.Setup(x => x.Equals(It.Is<IWindow>(y => y.GetHashCode() == hash))).Returns(true);
            return mock;
        }

    }
}
