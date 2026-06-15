using Module.Communication.Features.DeviceCommunicationConfig.Models;
using Shared.Infrastructure.PackMethod;
using System.IO;

namespace Module.Communication.Features.DeviceCommunicationConfig.Services;

public sealed class DeviceCommunicationStore
{
    private readonly Dictionary<DeviceCommunicationProfile, string> _profileStorageFileNames = new();

    public DeviceCommunicationStore(string configDirectory)
    {
        ConfigDirectory = configDirectory ?? throw new ArgumentNullException(nameof(configDirectory));
    }

    public string ConfigDirectory { get; }

    public int Load(
        Func<string?, bool> isSupportedCommunicationType,
        Action<DeviceCommunicationProfile> addProfile,
        Action<string> reportError)
    {
        ArgumentNullException.ThrowIfNull(isSupportedCommunicationType);
        ArgumentNullException.ThrowIfNull(addProfile);
        ArgumentNullException.ThrowIfNull(reportError);

        if (!Directory.Exists(ConfigDirectory))
        {
            return 0;
        }

        int loadedCount = 0;
        foreach (string filePath in Directory.EnumerateFiles(ConfigDirectory, "*.json").OrderBy(Path.GetFileName))
        {
            try
            {
                DeviceCommunicationProfileDocument? document =
                    JsonHelper.ReadJson<DeviceCommunicationProfileDocument>(filePath);
                if (document is null || !isSupportedCommunicationType(document.TypeId))
                {
                    continue;
                }

                DeviceCommunicationProfile profile = document.ToProfile();
                addProfile(profile);
                _profileStorageFileNames[profile] = Path.GetFileName(filePath);
                loadedCount++;
            }
            catch (Exception ex)
            {
                reportError($"读取通信配置失败：{Path.GetFileName(filePath)}。原因：{ex.Message}");
            }
        }

        return loadedCount;
    }

    public int Save(IEnumerable<DeviceCommunicationProfile> profiles)
    {
        Directory.CreateDirectory(ConfigDirectory);

        HashSet<string> usedFileNames = new(StringComparer.OrdinalIgnoreCase);
        int savedCount = 0;
        foreach (DeviceCommunicationProfile profile in profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.LocalName))
            {
                throw new InvalidOperationException("保存前必须填写配置名称。");
            }

            string fileName = BuildUniqueStorageFileName(profile.LocalName, usedFileNames);
            string filePath = Path.Combine(ConfigDirectory, fileName);
            JsonHelper.SaveJson(DeviceCommunicationProfileDocument.FromProfile(profile), filePath);

            if (_profileStorageFileNames.TryGetValue(profile, out string? oldFileName) &&
                !string.Equals(oldFileName, fileName, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteStorageFile(oldFileName);
            }

            _profileStorageFileNames[profile] = fileName;
            savedCount++;
        }

        return savedCount;
    }

    public void DeleteStoredProfileFile(DeviceCommunicationProfile profile)
    {
        if (!_profileStorageFileNames.TryGetValue(profile, out string? fileName))
        {
            return;
        }

        TryDeleteStorageFile(fileName);
        _profileStorageFileNames.Remove(profile);
    }

    private void TryDeleteStorageFile(string fileName)
    {
        try
        {
            string filePath = Path.Combine(ConfigDirectory, fileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
        }
    }

    private static string BuildUniqueStorageFileName(string localName, HashSet<string> usedFileNames)
    {
        string safeName = BuildSafeFileName(localName);
        string fileName = $"{safeName}.json";
        for (int index = 2; usedFileNames.Contains(fileName); index++)
        {
            fileName = $"{safeName}_{index}.json";
        }

        usedFileNames.Add(fileName);
        return fileName;
    }

    private static string BuildSafeFileName(string localName)
    {
        HashSet<char> invalidChars = new(Path.GetInvalidFileNameChars());
        System.Text.StringBuilder builder = new(localName.Trim().Length);
        foreach (char value in localName.Trim())
        {
            builder.Append(invalidChars.Contains(value) || char.IsControl(value)
                ? '_'
                : char.IsWhiteSpace(value) ? '_' : value);
        }

        string safeName = builder.ToString().Trim(' ', '.');
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "Communication";
        }

        return safeName.Length <= 80 ? safeName : safeName[..80];
    }
}
