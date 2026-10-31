namespace IronNebula;

public sealed partial class IronNebulaGame
{
    // Private-use tokens are rendered as vector icons, independent of the ASCII font atlas.
    private string MenuPrompt(string keyboard,string xbox,string sony)
        =>!_pad.IsConnected?keyboard:_usingDualSense?sony:xbox;
    private string ConfirmPrompt => MenuPrompt("ENTER / CLICK","\uE000","\uE004");
    private string BackPrompt => MenuPrompt("ESC","\uE001","\uE005");
    private string PagePrompt => MenuPrompt("TAB","\uE00C","\uE010");
    private string NavigatePrompt => MenuPrompt("ARROWS","\uE008 / \uE009","\uE008 / \uE009");
    private string HorizontalPrompt => MenuPrompt("LEFT / RIGHT","\uE008","\uE008");
    private string HelpPrompt => MenuPrompt("F1","\uE014","\uE016 / \uE017");
    private string SettingsPrompt => MenuPrompt("F12 / O","\uE003","\uE007");
    private string ContinuePrompt => MenuPrompt("C","\uE013","\uE015");
    private string CheckpointPrompt => MenuPrompt("C","\uE002","\uE006");
    private string ResumePrompt => MenuPrompt("ESC / "+_bindings[GameAction.Pause],"\uE013 / \uE001","\uE015 / \uE005");
}
