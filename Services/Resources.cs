using System.Resources;
using System.Reflection;

namespace CostCategorizationTool.Services;

/// <summary>
/// Strongly-typed access to localised strings.
/// Strings are stored in Resources.resx (English default) and Resources.fr.resx (French).
/// The active language is selected automatically via Thread.CurrentUICulture.
/// </summary>
internal static class Resources
{
    private static readonly ResourceManager _rm =
        new ResourceManager("CostCategorizationTool.Services.Resources",
                            Assembly.GetExecutingAssembly());

    private static string G(string name) => _rm.GetString(name) ?? name;

    // ── App ──────────────────────────────────────────────────────────────────
    public static string AppTitle          => G("AppTitle");
    public static string NewProject        => G("NewProject");
    public static string OpenProject       => G("OpenProject");
    public static string CloseProject      => G("CloseProject");
    public static string RecentProjects    => G("RecentProjects");
    public static string ImportCsv         => G("ImportCsv");
    public static string ViewSummary       => G("ViewSummary");
    public static string ExportExcel       => G("ExportExcel");
    public static string Exit              => G("Exit");
    public static string Tools             => G("Tools");
    public static string Settings          => G("Settings");
    public static string Close             => G("Close");
    public static string File              => G("File");

    // ── HomePanel ────────────────────────────────────────────────────────────
    public static string HomeTitle         => G("HomeTitle");
    public static string HomeSubtitle      => G("HomeSubtitle");
    public static string BtnNewProject     => G("BtnNewProject");
    public static string BtnOpenProject    => G("BtnOpenProject");
    public static string LblRecentProjects => G("LblRecentProjects");
    public static string NotFound          => G("NotFound");
    public static string FileNotFoundTitle => G("FileNotFoundTitle");
    public static string FileNotFoundMsg   => G("FileNotFoundMsg");
    public static string CreateProjectTitle=> G("CreateProjectTitle");
    public static string OpenProjectTitle  => G("OpenProjectTitle");
    public static string ProjectFilter     => G("ProjectFilter");
    public static string CsvFilter         => G("CsvFilter");

    // ── MainForm ─────────────────────────────────────────────────────────────
    public static string StepImport        => G("StepImport");
    public static string StepCategorize    => G("StepCategorize");
    public static string StepSummary       => G("StepSummary");
    public static string NoTransactionsYet => G("NoTransactionsYet");
    public static string NewProjectTitle   => G("NewProjectTitle");
    public static string ImportCompleteTitle => G("ImportCompleteTitle");
    public static string ImportCompleteMsg => G("ImportCompleteMsg");
    public static string ImportError       => G("ImportError");
    public static string ImportFailed      => G("ImportFailed");
    public static string NoProjectTitle    => G("NoProjectTitle");
    public static string OpenProjectFirst  => G("OpenProjectFirst");
    public static string CouldNotOpen      => G("CouldNotOpen");
    public static string ErrorTitle        => G("ErrorTitle");
    public static string SummaryTitle      => G("SummaryTitle");

    // ── TransactionCategorizationStep ────────────────────────────────────────
    public static string BtnAutoCateg      => G("BtnAutoCateg");
    public static string BtnManageCats     => G("BtnManageCats");
    public static string BtnSplitGroup     => G("BtnSplitGroup");
    public static string ChkUncategorized  => G("ChkUncategorized");
    public static string SelectGroupPrompt => G("SelectGroupPrompt");
    public static string GColCount         => G("GColCount");
    public static string GColType          => G("GColType");
    public static string GColPattern       => G("GColPattern");
    public static string GColDirection     => G("GColDirection");
    public static string GColFrequency     => G("GColFrequency");
    public static string GColTotal         => G("GColTotal");
    public static string GColCategory      => G("GColCategory");
    public static string DColDate          => G("DColDate");
    public static string DColAmount        => G("DColAmount");
    public static string DColCounterpart   => G("DColCounterpart");
    public static string DColDescription   => G("DColDescription");
    public static string DColCategory      => G("DColCategory");
    public static string ProgressFmt       => G("ProgressFmt");
    public static string DetailHeaderFmt   => G("DetailHeaderFmt");
    public static string TypeIban          => G("TypeIban");
    public static string TypeKeyword       => G("TypeKeyword");
    public static string CombineGroups     => G("CombineGroups");
    public static string CannotSplitTitle  => G("CannotSplitTitle");
    public static string CannotSplitMsg    => G("CannotSplitMsg");

    // ── SummaryStep ──────────────────────────────────────────────────────────
    public static string SumTitle          => G("SumTitle");
    public static string SumColCategory    => G("SumColCategory");
    public static string SumColCount       => G("SumColCount");
    public static string SumColTotal       => G("SumColTotal");
    public static string SumUncategorized  => G("SumUncategorized");
    public static string SumIncome         => G("SumIncome");
    public static string SumTotalExp       => G("SumTotalExp");
    public static string SumGrandTotal     => G("SumGrandTotal");
    public static string BtnExportCsv      => G("BtnExportCsv");
    public static string BtnExportExcel    => G("BtnExportExcel");
    public static string ExportTitle       => G("ExportTitle");
    public static string ExportFilter      => G("ExportFilter");
    public static string ExportExcelTitle  => G("ExportExcelTitle");
    public static string ExportExcelFilter => G("ExportExcelFilter");
    public static string ExportComplete    => G("ExportComplete");
    public static string ExportCompleteMsg => G("ExportCompleteMsg");
    public static string ExportError       => G("ExportError");
    public static string ExportFailed      => G("ExportFailed");

    // ── SettingsDialog ────────────────────────────────────────────────────────
    public static string SettTitle         => G("SettTitle");
    public static string SettLanguage      => G("SettLanguage");
    public static string SettLangNote      => G("SettLangNote");
    public static string SettMaintenance   => G("SettMaintenance");
    public static string BtnResetDb        => G("BtnResetDb");
    public static string BtnResetTx        => G("BtnResetTx");
    public static string ResetDbDesc       => G("ResetDbDesc");
    public static string ResetTxDesc       => G("ResetTxDesc");
    public static string ResetDbConfirmMsg => G("ResetDbConfirmMsg");
    public static string ResetDbConfirmTitle => G("ResetDbConfirmTitle");
    public static string ResetDbDoneMsg    => G("ResetDbDoneMsg");
    public static string ResetDbDoneTitle  => G("ResetDbDoneTitle");
    public static string ResetTxConfirmMsg => G("ResetTxConfirmMsg");
    public static string ResetTxConfirmTitle => G("ResetTxConfirmTitle");
    public static string ResetTxDoneMsg       => G("ResetTxDoneMsg");
    public static string ResetTxDoneTitle     => G("ResetTxDoneTitle");
    public static string FileAssocPromptTitle  => G("FileAssocPromptTitle");
    public static string FileAssocPromptMsg    => G("FileAssocPromptMsg");
    public static string FileAssocPromptYes    => G("FileAssocPromptYes");
    public static string FileAssocPromptNo     => G("FileAssocPromptNo");
    public static string FileAssocPromptLater  => G("FileAssocPromptLater");
    public static string SettFileAssoc         => G("SettFileAssoc");
    public static string BtnRegisterAssoc     => G("BtnRegisterAssoc");
    public static string BtnUnregisterAssoc   => G("BtnUnregisterAssoc");
    public static string FileAssocDesc        => G("FileAssocDesc");
    public static string FileAssocDone        => G("FileAssocDone");
    public static string FileAssocDoneTitle   => G("FileAssocDoneTitle");
    public static string FileAssocRemoved     => G("FileAssocRemoved");
    public static string FileAssocRemovedTitle => G("FileAssocRemovedTitle");

    // ── CategoryManagementStep ───────────────────────────────────────────────
    public static string CatDesc           => G("CatDesc");
    public static string CatTitle          => G("CatTitle");
    public static string CatListLabel      => G("CatListLabel");
    public static string RulesListLabel    => G("RulesListLabel");
    public static string BtnAdd            => G("BtnAdd");
    public static string BtnRename         => G("BtnRename");
    public static string BtnDelete         => G("BtnDelete");
    public static string BtnAddRule        => G("BtnAddRule");
    public static string BtnDeleteRule     => G("BtnDeleteRule");
    public static string RuleColType       => G("RuleColType");
    public static string RuleColPattern    => G("RuleColPattern");
    public static string RuleColDirection  => G("RuleColDirection");
    public static string SelectCatFirst    => G("SelectCatFirst");
    public static string SelectRuleFirst   => G("SelectRuleFirst");
    public static string NoCatSelectedTitle => G("NoCatSelectedTitle");
    public static string DeleteCatMsg      => G("DeleteCatMsg");
    public static string ConfirmDeleteTitle => G("ConfirmDeleteTitle");
    public static string DeleteRuleMsg     => G("DeleteRuleMsg");
    public static string AddRuleTitle      => G("AddRuleTitle");
    public static string RuleTypeLbl       => G("RuleTypeLbl");
    public static string RbIban            => G("RbIban");
    public static string RbDetails         => G("RbDetails");
    public static string PatternLbl        => G("PatternLbl");
    public static string DirectionLbl      => G("DirectionLbl");
    public static string DirAll            => G("DirAll");
    public static string DirIncoming       => G("DirIncoming");
    public static string DirOutgoing       => G("DirOutgoing");
    public static string BtnSaveRule       => G("BtnSaveRule");
    public static string Cancel            => G("Cancel");
    public static string ValidationTitle   => G("ValidationTitle");
    public static string EnterPattern      => G("EnterPattern");
    public static string NewCatName        => G("NewCatName");
    public static string AddCatTitle       => G("AddCatTitle");
    public static string NewNameLbl        => G("NewNameLbl");
    public static string RenameCatTitle    => G("RenameCatTitle");

    // ── View Menu ────────────────────────────────────────────────────────────
    public static string ViewMenu          => G("ViewMenu");
    public static string ViewUncategorized => G("ViewUncategorized");
    public static string ViewIncomingOnly  => G("ViewIncomingOnly");
    public static string ViewOutgoingOnly  => G("ViewOutgoingOnly");

    // ── Date Filter ──────────────────────────────────────────────────────────
    public static string DateFilterAll     => G("DateFilterAll");
    public static string DateFilterThisYear => G("DateFilterThisYear");
    public static string DateFilterLastYear => G("DateFilterLastYear");
    public static string DateFilterThisQuarter => G("DateFilterThisQuarter");
    public static string DateFilterLastQuarter => G("DateFilterLastQuarter");
    public static string DateFilterCustom  => G("DateFilterCustom");
    public static string DateFilterFrom    => G("DateFilterFrom");
    public static string DateFilterTo      => G("DateFilterTo");

    // ── Column Filter ────────────────────────────────────────────────────────
    public static string ColFilterMenuItem => G("ColFilterMenuItem");
    public static string ColFilterClear    => G("ColFilterClear");
    public static string ColFilterClearAll => G("ColFilterClearAll");
    public static string ColFilterPrompt   => G("ColFilterPrompt");
    public static string ColFilterTitle    => G("ColFilterTitle");

    // ── New Category ─────────────────────────────────────────────────────────
    public static string NewCategoryItem   => G("NewCategoryItem");
    public static string NewCatTitle       => G("NewCatTitle");
    public static string NewCatPrompt      => G("NewCatPrompt");

    // ── Frequency / Direction labels ─────────────────────────────────────────
    public static string FreqWeekly        => G("FreqWeekly");
    public static string FreqMonthly       => G("FreqMonthly");
    public static string FreqQuarterly     => G("FreqQuarterly");
    public static string FreqYearly        => G("FreqYearly");
    public static string FreqOccasional    => G("FreqOccasional");
    public static string DirExpense        => G("DirExpense");
    public static string DirIncome         => G("DirIncome");
    public static string DirMixed           => G("DirMixed");
    public static string SplitGroupTitle    => G("SplitGroupTitle");
    public static string SplitGroupLabel    => G("SplitGroupLabel");
    public static string SplitGroupInstr    => G("SplitGroupInstr");
    public static string SplitGroupManual   => G("SplitGroupManual");
    public static string SplitGroupPreview  => G("SplitGroupPreview");
    public static string SplitNoMatch       => G("SplitNoMatch");
    public static string SplitAllMatch      => G("SplitAllMatch");
    public static string SplitNoSelection   => G("SplitNoSelection");
    public static string ExportColStartDate => G("ExportColStartDate");
}
