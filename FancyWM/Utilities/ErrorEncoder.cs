using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace FancyWM.Utilities
{
    internal class ErrorEncoder
    {
        public static ErrorEncoder Default { get; } = new ErrorEncoder();

        public string GetErrorCodeString(Exception exception)
        {
            try
            {
                Exception source = exception.GetBaseException();
                var stackTrace = new StackTrace(source, true);

                var sourceFrame = FindOwnStackFrame(stackTrace) ?? stackTrace.GetFrame(0);
                if (sourceFrame == null)
                {
                    return "EUNK";
                }

                var components = new List<string>();

                var sourceFile = Path.GetFileNameWithoutExtension(sourceFrame.GetFileName());
                if (sourceFile != null)
                {
                    components.Add(Shorten(sourceFile));
                }
                else
                {
                    components.Add("E.");
                }

                var method = sourceFrame.GetMethod();
                if (method != null)
                {
                    if (method.DeclaringType != null)
                    {
                        var shortClassName = Shorten(method.DeclaringType.Name);
                        components.Add(shortClassName);
                    }
                    else
                    {
                        components.Add(".");
                    }

                    var shortMethodName = Shorten(method.Name);
                    components.Add(shortMethodName);
                }

                components.Add(sourceFrame.GetFileLineNumber().ToString());

                return string.Join('/', components);
            }
            catch (Exception)
            {
                return "EBAD";
            }
        }

        private static StackFrame? FindOwnStackFrame(StackTrace trace)
        {
            for (int i = 0; i < trace.FrameCount; i++)
            {
                var frame = trace.GetFrame(i);
                if (frame == null)
                    continue;
                var method = frame.GetMethod();
                if (method == null)
                    continue;
                var assemblyName = method.DeclaringType?.Assembly.GetName().FullName;
                if (assemblyName == null)
                    continue;

                if (assemblyName.Contains("FancyWM") || assemblyName.Contains("WinMan"))
                {
                    return frame;
                }
            }
            return null;
        }

        private static string Shorten(string s)
        {
            return new string(s
                .Where(ch => char.IsUpper(ch) || char.IsDigit(ch))
                .ToArray());
        }
    }
}
