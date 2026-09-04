using System.Windows;

namespace SoftwareCatalog.UI;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var application = new Application();
        application.Run();
    }
}
