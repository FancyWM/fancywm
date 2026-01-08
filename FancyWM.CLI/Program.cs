namespace FancyWM.CLI
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                DllImports.PInvoke.FreeConsole();
            }
            return Startup.Main(args);
        }
    }
}
