using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using System.Windows.Media;

namespace SubSpectra
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            Core.Initialize();

            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());

        }
    }
}