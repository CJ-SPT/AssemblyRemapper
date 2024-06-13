﻿using AssemblyRemapper.Enums;
using AssemblyRemapper.Models;
using AssemblyRemapper.Utils;
using Mono.Cecil;

namespace AssemblyRemapper.Reflection;

internal class Remapper
{
    public void InitializeRemap()
    {
        DisplayBasicModuleInformation();

        foreach (var remap in DataProvider.Remaps)
        {
            Logger.Log($"Finding best match for {remap.NewTypeName}...", ConsoleColor.Gray);

            HandleMapping(remap);
        }

        ChooseBestMatches();

        if (DataProvider.AppSettings.ScoringMode) { return; }

        // Dont publicize and unseal until after the remapping so we can use those as search parameters
        Publicizer.Publicize();
        Publicizer.Unseal();

        // We are done, write the assembly
        WriteAssembly();
    }

    private void DisplayBasicModuleInformation()
    {
        Logger.Log("-----------------------------------------------", ConsoleColor.Yellow);
        Logger.Log($"Starting remap...", ConsoleColor.Yellow);
        Logger.Log($"Module contains {DataProvider.ModuleDefinition.Types.Count} Types", ConsoleColor.Yellow);
        Logger.Log($"Publicize: {DataProvider.AppSettings.Publicize}", ConsoleColor.Yellow);
        Logger.Log($"Unseal: {DataProvider.AppSettings.Unseal}", ConsoleColor.Yellow);
        Logger.Log("-----------------------------------------------", ConsoleColor.Yellow);
    }

    private void HandleMapping(RemapModel mapping)
    {
        foreach (var type in DataProvider.ModuleDefinition.Types)
        {
            var result = ScoreType(type, mapping);

            if (result is not EFailureReason.None)
            {
                //Logger.LogDebug($"Remap [{type.Name} : {mapping.NewTypeName}] failed with reason {result}", silent: true);
            }
        }
    }

    private EFailureReason ScoreType(TypeDefinition type, RemapModel remap, string parentTypeName = "")
    {
        // Handle Direct Remaps by strict naming first bypasses everything else
        if (remap.UseForceRename)
        {
            HandleByDirectName(type, remap);
            return EFailureReason.None;
        }

        foreach (var nestedType in type.NestedTypes)
        {
            ScoreType(nestedType, remap, type.Name);
        }

        var score = new ScoringModel
        {
            ProposedNewName = remap.NewTypeName,
            RemapModel = remap,
            Definition = type,
        };

        // Set the original type name to be used later
        score.RemapModel.OriginalTypeName = type.Name;

        if (type.MatchIsAbstract(remap.SearchParams, score) == EMatchResult.NoMatch)
        {
            return EFailureReason.IsAbstract;
        }

        if (type.MatchIsEnum(remap.SearchParams, score) == EMatchResult.NoMatch)
        {
            return EFailureReason.IsEnum;
        }

        if (type.MatchIsNested(remap.SearchParams, score) == EMatchResult.NoMatch)
        {
            return EFailureReason.IsNested;
        }

        if (type.MatchIsSealed(remap.SearchParams, score) == EMatchResult.NoMatch)
        {
            return EFailureReason.IsSealed;
        }

        if (type.MatchIsDerived(remap.SearchParams, score) == EMatchResult.NoMatch)
        {
            return EFailureReason.IsDerived;
        }

        if (type.MatchIsInterface(remap.SearchParams, score) == EMatchResult.NoMatch)
        {
            return EFailureReason.IsInterface;
        }

        if (type.MatchHasGenericParameters(remap.SearchParams, score) == EMatchResult.NoMatch)
        {
            return EFailureReason.HasGenericParameters;
        }

        if (type.MatchIsPublic(remap.SearchParams, score) == EMatchResult.NoMatch)
        {
            return EFailureReason.IsPublic;
        }

        if (type.MatchHasAttribute(remap.SearchParams, score) == EMatchResult.NoMatch)
        {
            return EFailureReason.HasAttribute;
        }

        if (type.MatchMethods(remap.SearchParams, score) == EMatchResult.NoMatch)
        {
            return EFailureReason.HasMethods;
        }

        if (type.MatchFields(remap.SearchParams, score) == EMatchResult.NoMatch)
        {
            return EFailureReason.HasFields;
        }

        if (type.MatchProperties(remap.SearchParams, score) == EMatchResult.NoMatch)
        {
            return EFailureReason.HasProperties;
        }

        if (type.MatchNestedTypes(remap.SearchParams, score) == EMatchResult.NoMatch)
        {
            return EFailureReason.HasNestedTypes;
        }

        remap.OriginalTypeName = type.Name;
        score.AddScoreToResult();

        return EFailureReason.None;
    }

    private void HandleByDirectName(TypeDefinition type, RemapModel remap)
    {
        if (type.Name != remap.OriginalTypeName) { return; }

        var oldName = type.Name;
        type.Name = remap.NewTypeName;

        remap.OriginalTypeName = type.Name;

        Logger.Log("-----------------------------------------------", ConsoleColor.Green);
        Logger.Log($"Renamed {oldName} to {type.Name} directly", ConsoleColor.Green);

        Renamer.RenameAllDirect(remap, type);

        Logger.Log("-----------------------------------------------", ConsoleColor.Green);
    }

    private void ChooseBestMatches()
    {
        foreach (var score in DataProvider.ScoringModels)
        {
            ChooseBestMatch(score.Value, true);
        }
    }

    private void ChooseBestMatch(HashSet<ScoringModel> scores, bool isBest = false)
    {
        if (scores.Count == 0)
        {
            return;
        }

        var highestScore = scores.OrderByDescending(score => score.Score).FirstOrDefault();
        var nextHighestScores = scores.OrderByDescending(score => score.Score).Skip(1);

        if (highestScore is null) { return; }

        var potentialText = isBest
            ? "Best potential"
            : "Next potential";

        Logger.Log("-----------------------------------------------", ConsoleColor.Green);
        Logger.Log($"Renaming {highestScore.Definition.Name} to {highestScore.ProposedNewName}", ConsoleColor.Green);
        Logger.Log($"Max possible score: {highestScore.CalculateMaxScore()}", ConsoleColor.Green);
        Logger.Log($"Scored: {highestScore.Score} points", ConsoleColor.Green);

        if (scores.Count > 1)
        {
            Logger.Log($"Warning! There were {scores.Count - 1} possible matches. Considering adding more search parameters", ConsoleColor.Yellow);

            foreach (var score in scores.OrderByDescending(score => score.Score).Skip(1))
            {
                Logger.Log($"{score.Definition.Name} - Score [{score.Score}]", ConsoleColor.Yellow);
            }
        }

        highestScore.RemapModel.OriginalTypeName = highestScore.Definition.Name;

        // Rename type and all associated type members
        Renamer.RenameAll(highestScore);

        Logger.Log("-----------------------------------------------", ConsoleColor.Green);
    }

    private void WriteAssembly()
    {
        var filename = Path.GetFileNameWithoutExtension(DataProvider.AppSettings.AssemblyPath);
        var strippedPath = Path.GetDirectoryName(filename);

        filename = $"{filename}-Remapped.dll";

        var remappedPath = Path.Combine(strippedPath, filename);

        DataProvider.AssemblyDefinition.Write(remappedPath);
        DataProvider.UpdateMapping();

        Logger.Log("-----------------------------------------------", ConsoleColor.Green);
        Logger.Log($"Complete: Assembly written to `{remappedPath}`", ConsoleColor.Green);
        Logger.Log("Original type names updated on mapping file.", ConsoleColor.Green);
        Logger.Log("-----------------------------------------------", ConsoleColor.Green);
    }
}