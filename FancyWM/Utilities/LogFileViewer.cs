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

    class LogFileViewer : ILogFileViewer
    {
        private readonly string m_fileName;

        public ICollection<string> TextContent { get; }

        public LogFileViewer(string fileName, ICollection<string> textContent)
        {
            m_fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            TextContent = textContent ?? throw new ArgumentNullException(nameof(textContent));
        }

        public void Open()
        {
            Process.Start("explorer.exe", $"/select, {m_fileName}");
        }
    }
}
