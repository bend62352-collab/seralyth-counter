using System.Collections;
using System.Reflection;
using BepInEx;
using hamburbur.Plugins;

namespace SeralythCounter;

[BepInDependency("org.hamburbur.menu")]
[BepInPlugin(Constants.Guid, Constants.Name, Constants.Version)]
public class Plugin : BaseUnityPlugin
{
    private IEnumerator Start()
    {
        while (hamburbur.Plugin.Instance == null     ||
               !hamburbur.Plugin.Instance.MenuLoaded ||
               PluginManager.Instance == null)
            yield return null;

        PluginManager.Instance.RegisterAssembly(Assembly.GetExecutingAssembly());
    }
}

[HamburburPlugin(                  Constants.Guid, Constants.Name, Constants.Version,
        Author = Constants.Author, Description = Constants.Description)]
public sealed class SeralythCounterPlugin : IHamburburPlugin
{
    public void Load(PluginContext context)
    {
        HarmonyPatches.ApplyHarmonyPatches();
    }

    public void Unload() => HarmonyPatches.RemoveHarmonyPatches();
}
