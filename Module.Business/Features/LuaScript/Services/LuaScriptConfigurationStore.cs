using Module.Business.Features.LuaScript;
using Module.Business.Models;
using Shared.Infrastructure.Extensions;
using Shared.Infrastructure.PackMethod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Module.Business.Services;

public sealed record LuaScriptProfileLoadResult(
    LuaScriptProfile Profile,
    string StorageFileName);

public sealed record LuaScriptProfileSaveResult(
    LuaScriptProfile Profile,
    string StorageFileName);

public static class LuaScriptConfigurationStore
{
    private static readonly string LuaScriptConfigDirectory =
        Path.Combine(AppContext.BaseDirectory, "Config", "LuaScript");

    public static IReadOnlyList<LuaScriptProfileLoadResult> LoadProfiles()
    {
        if (!Directory.Exists(LuaScriptConfigDirectory))
        {
            return Array.Empty<LuaScriptProfileLoadResult>();
        }

        List<LuaScriptProfileLoadResult> results = new();
        foreach (string filePath in Directory.EnumerateFiles(LuaScriptConfigDirectory, "*.json").OrderBy(Path.GetFileName))
        {
            string storageText = File.ReadAllText(filePath, Encoding.UTF8);
            LuaScriptProfileDocument? document = DeserializeProfileDocument(storageText);
            if (document is null)
            {
                continue;
            }

            results.Add(new LuaScriptProfileLoadResult(
                document.ToProfile(),
                Path.GetFileName(filePath)));
        }

        return results;
    }

    public static IReadOnlyList<LuaScriptProfileSaveResult> SaveProfiles(
        IEnumerable<LuaScriptProfile> profiles,
        IReadOnlyDictionary<LuaScriptProfile, string> existingFileNames)
    {
        Directory.CreateDirectory(LuaScriptConfigDirectory);

        List<LuaScriptProfile> profileList = profiles.ToList();
        HashSet<string> usedFileNames = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<LuaScriptProfile, string> targetFileNames = new();
        foreach (LuaScriptProfile profile in profileList)
        {
            ValidateProfileForSave(profile);
            targetFileNames[profile] = BuildUniqueStorageFileName(profile.Name, usedFileNames);
        }

        List<LuaScriptProfileSaveResult> results = new();
        foreach (LuaScriptProfile profile in profileList)
        {
            string fileName = targetFileNames[profile];
            string filePath = Path.Combine(LuaScriptConfigDirectory, fileName);
            string storageText = JsonHelper.SerializeObject(LuaScriptProfileDocument.FromProfile(profile));
            File.WriteAllText(filePath, storageText, Encoding.UTF8);
            results.Add(new LuaScriptProfileSaveResult(profile, fileName));
        }

        foreach (LuaScriptProfile profile in profileList)
        {
            string fileName = targetFileNames[profile];
            if (existingFileNames.TryGetValue(profile, out string? oldFileName) &&
                !string.Equals(oldFileName, fileName, StringComparison.OrdinalIgnoreCase) &&
                !usedFileNames.Contains(oldFileName))
            {
                TryDeleteStorageFile(oldFileName);
            }
        }

        return results;
    }

    public static void DeleteProfileFile(string fileName)
    {
        TryDeleteStorageFile(fileName);
    }

    private static LuaScriptProfileDocument? DeserializeProfileDocument(string storageText)
    {
        try
        {
            return JsonHelper.DeserializeObject<LuaScriptProfileDocument>(storageText);
        }
        catch
        {
            return JsonHelper.DeserializeObject<LuaScriptProfileDocument>(storageText.DesDecrypt());
        }
    }

    private static void ValidateProfileForSave(LuaScriptProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new InvalidOperationException("Lua script name cannot be empty.");
        }
    }

    private static void TryDeleteStorageFile(string fileName)
    {
        try
        {
            string filePath = Path.Combine(LuaScriptConfigDirectory, fileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
        }
    }

    private static string BuildUniqueStorageFileName(string profileName, HashSet<string> usedFileNames)
    {
        string safeName = BuildSafeFileName(profileName);
        string fileName = $"{safeName}.json";
        for (int index = 2; usedFileNames.Contains(fileName); index++)
        {
            fileName = $"{safeName}_{index}.json";
        }

        usedFileNames.Add(fileName);
        return fileName;
    }

    private static string BuildSafeFileName(string value)
    {
        HashSet<char> invalidChars = new(Path.GetInvalidFileNameChars());
        StringBuilder builder = new(value.Trim().Length);
        foreach (char current in value.Trim())
        {
            builder.Append(invalidChars.Contains(current) || char.IsControl(current)
                ? '_'
                : char.IsWhiteSpace(current) ? '_' : current);
        }

        string safeName = builder.ToString().Trim(' ', '.');
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "LuaScript";
        }

        return safeName.Length <= 80 ? safeName : safeName[..80];
    }
}
