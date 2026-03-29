using System.Globalization;
using System.Text;
using CostCategorizationTool.Forms;
using CostCategorizationTool.Models;

namespace CostCategorizationTool;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ApplicationConfiguration.Initialize();
        var settings = AppSettings.Load();

        var lang = settings.Language;
        if (lang == null)
        {
            var detected = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLower();
            lang = detected == "fr" ? "fr" : "en";
        }
        Thread.CurrentThread.CurrentUICulture = new CultureInfo(lang);

        // When launched by double-clicking a .ccp file, Windows passes the file
        // path as the first command-line argument.
        string? startupFile = args.Length > 0 && File.Exists(args[0]) ? args[0] : null;
        Application.Run(new MainForm(startupFile));
    }
}
