using System;

namespace FancyWM.GUI
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            return Startup.Main(args);
        }
    }
}
