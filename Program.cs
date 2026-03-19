using System.Globalization;
using CostCategorizationTool.Forms;
using CostCategorizationTool.Models;

namespace CostCategorizationTool;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        var settings = AppSettings.Load();

        var lang = settings.Language;
        if (lang == null)
        {
            var detected = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLower();
            lang = detected == "fr" ? "fr" : "en";
        }
        Thread.CurrentThread.CurrentUICulture = new CultureInfo(lang);

        Application.Run(new MainForm());
    }
}
