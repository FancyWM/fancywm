using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FancyWM.Utilities
{
    class LineWriter : TextWriter
    {
        private readonly StringBuilder m_line = new StringBuilder();

        public ICollection<string> OutputCollection { get; }

        public char NewLineCharacter { get; }

        public override Encoding Encoding => Encoding.Default;

        public LineWriter(ICollection<string> outputCollection, char newLineCharacter)
        {
            OutputCollection = outputCollection ?? throw new ArgumentNullException(nameof(outputCollection));
            NewLineCharacter = newLineCharacter;
        }

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
