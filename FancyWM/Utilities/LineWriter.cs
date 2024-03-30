using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FancyWM.Utilities
{
    class LineWriter(ICollection<string> outputCollection, char newLineCharacter) : TextWriter
    {
        private readonly StringBuilder m_line = new();

        public ICollection<string> OutputCollection { get; } = outputCollection ?? throw new ArgumentNullException(nameof(outputCollection));

        public char NewLineCharacter { get; } = newLineCharacter;

        public override Encoding Encoding => Encoding.Default;

        public override void Write(char value)
        {
            if (value == '\n')
            {
                var line = m_line.ToString();
                m_line.Clear();
                lock (OutputCollection)
                {
                    OutputCollection.Add(line);
                }
            }
            else
            {
                m_line.Append(value);
            }
        }
    }
}
