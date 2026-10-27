using System;
using System.Collections;
using System.IO;
using System.Reflection;

// Exercises the installed engine's real hook resolver without starting Unity.
// CustomAttributeData inspects the bootstrap marker without running its code.
public static class HookResolutionCheck
{
    public static int Main(string[] args)
    {
        try
        {
            string game = Path.GetFullPath(args[0]);
            string radarDll = Path.GetFullPath(args[1]);
            bool legacy = args.Length > 2 && args[2] == "--expect-legacy";
            string managed = Path.Combine(game, "x64/FF9_Data/Managed");
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e) {
                string path = Path.Combine(managed, new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            };
            Assembly engine = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
            Type loader = engine.GetType("Memoria.Scripts.ScriptsLoader", true);
            Type result = loader.GetNestedType("Result", BindingFlags.NonPublic);
            Type hook = engine.GetType("Memoria.IOverloadOnBattleInitScript", true);
            FieldInfo results = loader.GetField("s_result", BindingFlags.NonPublic | BindingFlags.Static);
            Type floating = Assembly.LoadFrom(Path.Combine(game, "FloatingHPBar/StreamingAssets/Scripts/Memoria.Scripts.FloatingHPBar.dll")).GetType("FloatingHPBar.FloatingHPBarBootstrap", true);
            Assembly radar = Assembly.LoadFrom(radarDll);
            Type chocobo = radar.GetType("ChocoboHotCold.ChocoboHotColdBootstrap", true);
            if (hook.IsAssignableFrom(chocobo) != legacy) throw new Exception("Unexpected battle-init hook registration");
            if (!hook.IsAssignableFrom(floating)) throw new Exception("FloatingHPBar fixture has no battle-init hook");
            bool hasBootstrap = false;
            foreach (CustomAttributeData attribute in CustomAttributeData.GetCustomAttributes(chocobo))
                if (attribute.AttributeType.FullName == "ChocoboHotCold.ChocoboHotColdBootstrapAttribute") hasBootstrap = true;
            if (!hasBootstrap) throw new Exception("Independent startup attribute is missing");
            Type overlay = radar.GetType("ChocoboHotCold.ChocoboHotColdOverlay", true);
            if (overlay.GetMethod("InstallMainThreadPump") == null || overlay.GetMethod("Ensure") == null)
                throw new Exception("Independent startup entry points are missing");
            if (chocobo.GetMethod("OnBattleInit") != null && !legacy) throw new Exception("Obsolete battle fallback remains");
            Check(loader, result, hook, results, new Type[] { floating, chocobo }, floating);
            Check(loader, result, hook, results, new Type[] { chocobo, floating }, legacy ? chocobo : floating);
            Check(loader, result, hook, results, new Type[] { floating }, floating);
            Check(loader, result, hook, results, new Type[] { chocobo }, legacy ? chocobo : null);
            Console.WriteLine(legacy ? "PASS: v1.0 load-order dependency reproduced." : "PASS: both orders preserve FloatingHPBar's hook; radar-only mode claims no battle hook; independent startup marker remains.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void Check(Type loader, Type resultType, Type hook, FieldInfo results, Type[] order, Type expected)
    {
        IList list = (IList)Activator.CreateInstance(results.FieldType);
        foreach (Type implementation in order)
        {
            object result = Activator.CreateInstance(resultType, new object[] { implementation.Assembly.Location });
            IDictionary hooks = (IDictionary)resultType.GetField("OverloadableMethodScripts").GetValue(result);
            foreach (Type implemented in implementation.GetInterfaces())
                if (implemented == hook) hooks.Add(hook, implementation);
            list.Add(result);
        }
        results.SetValue(null, list);
        object selected = loader.GetMethod("GetOverloadedMethod").Invoke(null, new object[] { hook });
        Type actual = selected == null ? null : selected.GetType();
        if (actual != expected) throw new Exception("Incorrect hook selection");
        Console.WriteLine(String.Join(" > ", Array.ConvertAll(order, delegate(Type t) { return t.Name; })) + " => " + (actual == null ? "none" : actual.FullName));
    }
}
