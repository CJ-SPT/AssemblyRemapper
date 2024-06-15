﻿using Mono.Cecil;
using Newtonsoft.Json;
using ReCodeIt.Models;

namespace ReCodeIt.Utils;

public static class DataProvider
{
    static DataProvider()
    {
    }

    public static List<RemapModel> Remaps { get; private set; } = [];

    public static Dictionary<string, HashSet<ScoringModel>> ScoringModels { get; set; } = [];

    public static Settings Settings { get; private set; }

    public static AssemblyDefinition AssemblyDefinition { get; private set; }

    public static ModuleDefinition ModuleDefinition { get; private set; }

    public static void LoadAppSettings()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "Data", "Settings.jsonc");

        if (!File.Exists(settingsPath))
        {
            throw new FileNotFoundException($"path `{settingsPath}` does not exist...");
        }

        var jsonText = File.ReadAllText(settingsPath);

        JsonSerializerSettings settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        Settings = JsonConvert.DeserializeObject<Settings>(jsonText, settings);

        Logger.Log($"Settings loaded from '{settingsPath}'");
    }

    public static void SaveAppSettings()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "Data", "Settings.jsonc");

        if (!File.Exists(settingsPath))
        {
            throw new FileNotFoundException($"path `{settingsPath}` does not exist...");
        }

        JsonSerializerSettings settings = new()
        {
            Formatting = Formatting.Indented
        };

        var jsonText = JsonConvert.SerializeObject(Settings, settings);

        File.WriteAllText(settingsPath, jsonText);
    }

    public static void LoadMappingFile(string path = "")
    {
        if (!File.Exists(Settings.AppSettings.MappingPath))
        {
            throw new InvalidOperationException($"path `{Settings.AppSettings.MappingPath}` does not exist...");
        }

        var fpath = path == string.Empty
            ? Settings.AppSettings.MappingPath
            : path;

        var jsonText = File.ReadAllText(fpath);

        Remaps = [];
        ScoringModels = [];

        Remaps = JsonConvert.DeserializeObject<List<RemapModel>>(jsonText);

        var properties = typeof(SearchParams).GetProperties();

        foreach (var remap in Remaps)
        {
            foreach (var property in properties)
            {
                if (property.PropertyType == typeof(List<string>) && property.GetValue(remap.SearchParams) is null)
                {
                    property.SetValue(remap.SearchParams, new List<string>());
                }
            }
        }

        Logger.Log($"Mapping file loaded from '{Settings.AppSettings.MappingPath}'");
    }

    public static void SaveMapping()
    {
        JsonSerializerSettings settings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented
        };

        var jsonText = JsonConvert.SerializeObject(Remaps, settings);

        File.WriteAllText(Settings.AppSettings.MappingPath, jsonText);
    }

    public static void UpdateMapping()
    {
        if (!File.Exists(Settings.AppSettings.MappingPath))
        {
            throw new FileNotFoundException($"path `{Settings.AppSettings.MappingPath}` does not exist...");
        }

        JsonSerializerSettings settings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented
        };

        var properties = typeof(SearchParams).GetProperties();

        foreach (var remap in Remaps)
        {
            foreach (var property in properties)
            {
                if (property.PropertyType == typeof(List<string>))
                {
                    var val = property.GetValue(remap.SearchParams);

                    if (val is List<string> list && list.Count > 0) { continue; }

                    property.SetValue(remap.SearchParams, null);
                }
            }
        }

        var jsonText = JsonConvert.SerializeObject(Remaps, settings);

        File.WriteAllText(Settings.AppSettings.MappingPath, jsonText);

        Logger.Log($"Mapping file saved to {Settings.AppSettings.MappingPath}");
    }

    public static void LoadAssemblyDefinition()
    {
        AssemblyDefinition = null;
        ModuleDefinition = null;

        DefaultAssemblyResolver resolver = new();
        resolver.AddSearchDirectory(Path.GetDirectoryName(Settings.AppSettings.AssemblyPath)); // Replace with the correct path : (6/14) I have no idea what I met by that
        ReaderParameters parameters = new() { AssemblyResolver = resolver };

        AssemblyDefinition = AssemblyDefinition.ReadAssembly(Settings.AppSettings.AssemblyPath, parameters);

        if (AssemblyDefinition is null)
        {
            throw new NullReferenceException("AssemblyDefinition was null...");
        }

        var fileName = Path.GetFileName(Settings.AppSettings.AssemblyPath);

        foreach (var module in AssemblyDefinition.Modules.ToArray())
        {
            if (module.Name == fileName)
            {
                Logger.Log($"Module definition {module.Name} found'");
                ModuleDefinition = module;
                return;
            }
        }

        Logger.Log($"Module `{fileName}` not found in assembly {fileName}");
    }
}