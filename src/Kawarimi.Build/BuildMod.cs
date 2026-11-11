using Kawarimi.Formats;
using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;

namespace Kawarimi.Build;

public sealed class BuildMod : Task
{
    [Required]
    public string ProjectDirectory { get; set; } = "";

    [Required]
    public string Assembly { get; set; } = "";

    [Required]
    public string ModsDirectory { get; set; } = "";

    public override bool Execute()
    {
        try
        {
            var info = ModProject.ReadInfo(ProjectDirectory);
            string modDir = Path.Combine(ModsDirectory, info.Name);
            Directory.CreateDirectory(modDir);
            foreach (var b in ModProject.BuildFiles(ProjectDirectory, modDir))
            {
                if (b.Problem != null) Log.LogWarning($"{b.Name}: {b.Problem}");
                else if (b.Changed) Log.LogMessage(MessageImportance.High, $"  {b.Name}: {b.Textures} texture(s), {b.Meshes} mesh group(s) changed");
                if (b.Note != null) Log.LogMessage(MessageImportance.High, $"  {b.Name}: {b.Note}");
            }
            File.Copy(Assembly, Path.Combine(modDir, info.Name + ".dll"), overwrite: true);
            File.Delete(Path.Combine(modDir, ModProject.ManifestFile));
            Log.LogMessage(MessageImportance.High, $"Kawarimi mod ready: {modDir}");
            return !Log.HasLoggedErrors;
        }
        catch (Exception e)
        {
            Log.LogError($"Kawarimi: {e.Message}");
            return false;
        }
    }
}
