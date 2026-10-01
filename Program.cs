using System;
using System.Windows.Forms;

namespace VRChatInstanceLogger
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // IMPORTANT: This MUST match your form name
            Application.Run(new VRChatInstanceLogger());
        }
    }
}
