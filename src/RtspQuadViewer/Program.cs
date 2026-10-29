using LibVLCSharp.Shared;

namespace RtspQuadViewer;

static class Program
{
    [STAThread]
    static void Main()
    {
        // libvlc is shipped next to the app (or in the single-file extraction dir).
        Core.Initialize(Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64"));

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());

        // libvlc keeps native worker threads alive; exit hard so closing the window always ends the process.
        Environment.Exit(0);
    }
}
