﻿using dnlib.DotNet;
using ReCodeIt.Models;
using ReCodeIt.Utils;

namespace ReCodeIt.ReMapper;

internal static class RenameHelper
{
    private static List<string> TokensToMatch => DataProvider.Settings.AutoMapper.TokensToMatch;

    /// <summary>
    /// Only used by the manual remapper, should probably be removed
    /// </summary>
    /// <param name="score"></param>
    public static void RenameAll(ModuleDefMD module, ScoringModel score, bool direct = false)
    {
        var types = module.GetTypes();

        // Rename all fields and properties first
        if (DataProvider.Settings.Remapper.MappingSettings.RenameFields)
        {
            RenameAllFields(score.Definition.Name, score.ReMap.NewTypeName, types);
        }

        if (DataProvider.Settings.Remapper.MappingSettings.RenameProperties)
        {
            RenameAllProperties(score.Definition.Name, score.ReMap.NewTypeName, types);
        }

        if (!direct)
        {
            RenameType(types, score);
        }

        Logger.Log($"{score.Definition.Name} Renamed.", ConsoleColor.Green);
    }

    /// <summary>
    /// Only used by the manual remapper, should probably be removed
    /// </summary>
    /// <param name="score"></param>
    public static void RenameAllDirect(ModuleDefMD module, RemapModel remap, TypeDef type)
    {
        var directRename = new ScoringModel
        {
            Definition = type,
            ReMap = remap
        };
        RenameAll(module, directRename, true);
    }

    /// <summary>
    /// Rename all fields recursively, returns number of fields changed
    /// </summary>
    /// <param name="oldTypeName"></param>
    /// <param name="newTypeName"></param>
    /// <param name="typesToCheck"></param>
    /// <returns></returns>
    public static int RenameAllFields(
        string oldTypeName,
        string newTypeName,
        IEnumerable<TypeDef> typesToCheck,
        int overAllCount = 0)
    {
        foreach (var type in typesToCheck)
        {
            var fields = type.Fields
                .Where(field => field.Name.IsFieldOrPropNameInList(TokensToMatch));

            if (!fields.Any()) { continue; }

            int fieldCount = 0;
            foreach (var field in fields)
            {
                if (field.Name == oldTypeName)
                {
                    var newFieldName = GetNewFieldName(newTypeName, field.IsPrivate, fieldCount);

                    // Dont need to do extra work
                    if (field.Name == newFieldName) { continue; }

                    Logger.Log($"Renaming original field type name: `{field.Name}` with name `{field.Name}` to `{newFieldName}`", ConsoleColor.Green);

                    field.Name = newFieldName;

                    fieldCount++;
                    overAllCount++;
                }
            }

            if (type.HasNestedTypes)
            {
                RenameAllFields(oldTypeName, newTypeName, type.NestedTypes, overAllCount);
            }
        }

        return overAllCount;
    }

    /// <summary>
    /// Rename all properties recursively, returns number of fields changed
    /// </summary>
    /// <param name="oldTypeName"></param>
    /// <param name="newTypeName"></param>
    /// <param name="typesToCheck"></param>
    /// <returns></returns>
    public static int RenameAllProperties(
        string oldTypeName,
        string newTypeName,
        IEnumerable<TypeDef> typesToCheck,
        int overAllCount = 0)
    {
        foreach (var type in typesToCheck)
        {
            var properties = type.Properties
                .Where(prop => prop.Name.IsFieldOrPropNameInList(TokensToMatch));

            if (!properties.Any()) { continue; }

            int propertyCount = 0;
            foreach (var property in properties)
            {
                if (property.Name == oldTypeName)
                {
                    var newPropertyName = GetNewPropertyName(newTypeName, propertyCount);

                    // Dont need to do extra work
                    if (property.Name == newPropertyName) { continue; }

                    Logger.Log($"Renaming original property type name: `{property.Name}` with name `{property.Name}` to `{newPropertyName}`", ConsoleColor.Green);
                    property.Name = newPropertyName;
                    propertyCount++;
                    overAllCount++;
                }
            }

            if (type.HasNestedTypes)
            {
                RenameAllProperties(oldTypeName, newTypeName, type.NestedTypes, overAllCount);
            }
        }

        return overAllCount;
    }

    public static string GetNewFieldName(string NewName, bool isPrivate, int fieldCount = 0)
    {
        var discard = isPrivate ? "_" : "";
        string newFieldCount = fieldCount > 0 ? $"_{fieldCount}" : string.Empty;

        return $"{discard}{char.ToLower(NewName[0])}{NewName[1..]}{newFieldCount}";
    }

    public static string GetNewPropertyName(string newName, int propertyCount = 0)
    {
        return propertyCount > 0 ? $"{newName}_{propertyCount}" : newName;
    }

    private static void RenameType(IEnumerable<TypeDef> typesToCheck, ScoringModel score)
    {
        foreach (var type in typesToCheck)
        {
            if (type.HasNestedTypes)
            {
                RenameType(type.NestedTypes, score);
            }

            if (score.Definition.Name is null) { continue; }

            if (score.ReMap.SearchParams.IsNested is true &&
                type.IsNested && type.Name == score.Definition.Name)
            {
                type.Name = score.ProposedNewName;
            }

            if (type.FullName == score.Definition.Name)
            {
                type.Name = score.ProposedNewName;
            }
        }
    }
}