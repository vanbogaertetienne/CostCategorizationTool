using CostCategorizationTool.Forms;

namespace CostCategorizationTool;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new WizardForm());
    }
}
