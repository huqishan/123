using Module.Communication.Features.ProtocolConfig.Models;
using Shared.Infrastructure.Extensions;
using Shared.Infrastructure.PackMethod;
using System.IO;
using System.Text;

namespace Module.Communication.Features.ProtocolConfig.Services;

public sealed class ProtocolStore
{
    private readonly Dictionary<ProtocolConfigProfile, string> _profileStorageFileNames = new();

    public ProtocolStore(string configDirectory)
    {
        ConfigDirectory = configDirectory ?? throw new ArgumentNullException(nameof(configDirectory));
    }

    public string ConfigDirectory { get; }

    public int Load(Action<ProtocolConfigProfile> addProfile, Action<string> reportError)
    {
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
                string storageText = File.ReadAllText(filePath, Encoding.UTF8);
                ProtocolConfigProfileDocument? document =
                    JsonHelper.DeserializeObject<ProtocolConfigProfileDocument>(storageText.DesDecrypt());
                if (document is null)
                {
                    continue;
                }

                ProtocolConfigProfile profile = document.ToProfile();
                addProfile(profile);
                _profileStorageFileNames[profile] = Path.GetFileName(filePath);
                loadedCount++;
            }
            catch (Exception ex)
            {
                reportError($"读取协议配置失败：{Path.GetFileName(filePath)}，原因：{ex.Message}");
            }
        }

        return loadedCount;
    }

    public int Save(IEnumerable<ProtocolConfigProfile> profiles)
    {
        Directory.CreateDirectory(ConfigDirectory);

        HashSet<string> usedFileNames = new(StringComparer.OrdinalIgnoreCase);
        int savedCount = 0;
        foreach (ProtocolConfigProfile profile in profiles)
        {
            ValidateProfileForSave(profile);
            if (!ProtocolPreviewEngine.TryRefreshParsedResultKeys(profile, out string parseMessage))
            {
                throw new InvalidOperationException(parseMessage);
            }

            string fileName = BuildUniqueStorageFileName(profile.Name, usedFileNames);
            string filePath = Path.Combine(ConfigDirectory, fileName);
            string storageText = JsonHelper.SerializeObject(ProtocolConfigProfileDocument.FromProfile(profile)).Encrypt();
            File.WriteAllText(filePath, storageText, Encoding.UTF8);

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

    public void DeleteStoredProfileFile(ProtocolConfigProfile profile)
    {
        if (!_profileStorageFileNames.TryGetValue(profile, out string? fileName))
        {
            return;
        }

        TryDeleteStorageFile(fileName);
        _profileStorageFileNames.Remove(profile);
    }

    private static void ValidateProfileForSave(ProtocolConfigProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new InvalidOperationException("协议名称不能为空。");
        }

        if (profile.Commands.Count == 0)
        {
            throw new InvalidOperationException($"协议 {profile.Name} 至少需要包含一条指令。");
        }

        foreach (ProtocolCommandConfig command in profile.Commands)
        {
            if (string.IsNullOrWhiteSpace(command.Name))
            {
                throw new InvalidOperationException($"协议 {profile.Name} 存在未命名指令。");
            }

            if (!int.TryParse(command.ReplyAggregationMilliseconds.Trim(), out int replyWait) || replyWait < 0)
            {
                throw new InvalidOperationException($"指令 {command.Name} 的强制等待拼接时长必须是大于等于 0 的整数毫秒。");
            }
        }
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
            // Old files can be cleaned manually if deletion fails.
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
            safeName = "Protocol";
        }

        return safeName.Length <= 80 ? safeName : safeName[..80];
    }
}
