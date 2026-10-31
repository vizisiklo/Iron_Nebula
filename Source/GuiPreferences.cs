using System;
using System.IO;
using System.Text.Json;

namespace IronNebula;

internal static class GuiPreferences
{
    internal sealed record Settings(bool Vsync=true,bool Dof=true,bool CombatFx=true,
        bool EnemyOutlines=true,bool WeaponVfx=true,bool Fullscreen=false, int Difficulty=1,
        int ResolutionIndex=1,int EnemyShotGlow=50,int HitEffectStrength=55,int Antialiasing=0,
        bool ControllerHaptics=true,bool AdaptiveTriggers=true,int ControllerStrength=65,bool BluetoothHaptics=false);

    private static string PathForSettings()
    {
        string root=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root,"IronNebula","gui-settings.json");
    }

    public static Settings Read()
    {
        try
        {
            string path=PathForSettings();
            return File.Exists(path)? JsonSerializer.Deserialize<Settings>(File.ReadAllText(path))??new Settings():new Settings();
        }
        catch {return new Settings();}
    }

    public static void Write(Settings value)
    {
        try
        {
            string path=PathForSettings();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path,JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}));
        }
        catch { /* Settings may be unavailable in restricted profiles; never crash gameplay. */ }
    }
}
