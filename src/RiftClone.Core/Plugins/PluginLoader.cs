using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace RiftClone.Core.Plugins;

/// <summary>
/// يفحص مجلد "Plugins" ويحمّل أي DLL بداخله، ثم يكتشف أي Class يطبّق
/// IGameLibraryProvider أو IGameTool ويسجّله تلقائيًا.
/// هذا هو قلب فكرة التوسّع: لإضافة ميزة جديدة مستقبلاً (منصة جديدة، أداة جديدة
/// مثل OptiScaler)، تكفي إضافة DLL جديد لهذا المجلد بدون لمس باقي الكود.
/// </summary>
public class PluginLoader
{
    private readonly string _pluginsDirectory;

    public List<IGameLibraryProvider> LibraryProviders { get; } = new();
    public List<IGameTool> GameTools { get; } = new();

    public PluginLoader(string pluginsDirectory)
    {
        _pluginsDirectory = pluginsDirectory;
    }

    public void LoadPlugins()
    {
        if (!Directory.Exists(_pluginsDirectory))
        {
            Directory.CreateDirectory(_pluginsDirectory);
            return;
        }

        foreach (var dllPath in Directory.GetFiles(_pluginsDirectory, "*.dll"))
        {
            try
            {
                var loadContext = new PluginLoadContext(dllPath);
                var assembly = loadContext.LoadFromAssemblyPath(dllPath);

                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsInterface || type.IsAbstract) continue;

                    if (typeof(IGameLibraryProvider).IsAssignableFrom(type))
                    {
                        if (Activator.CreateInstance(type) is IGameLibraryProvider provider)
                            LibraryProviders.Add(provider);
                    }

                    if (typeof(IGameTool).IsAssignableFrom(type))
                    {
                        if (Activator.CreateInstance(type) is IGameTool tool)
                            GameTools.Add(tool);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"فشل تحميل الإضافة {Path.GetFileName(dllPath)}: {ex.Message}");
            }
        }
    }
}

/// سياق تحميل معزول لكل Plugin، يحل تبعياته الخاصة بشكل مستقل
internal class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path != null ? LoadFromAssemblyPath(path) : null;
    }
}
