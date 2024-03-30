using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace FancyWM.Utilities
{
    interface ILogFileViewer
    {
        ICollection<string> TextContent { get; }

        void Open();
    }

    class LogFileViewer(string fileName, ICollection<string> textContent) : ILogFileViewer
    {
        private readonly string m_fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));

        public ICollection<string> TextContent { get; } = textContent ?? throw new ArgumentNullException(nameof(textContent));

        public void Open()
        {
            Process.Start("explorer.exe", $"/select, {m_fileName}");
        }
    }
}
