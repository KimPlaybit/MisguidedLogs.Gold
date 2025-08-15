using BunnyCDN.Net.Storage;
using BunnyCDN.Net.Storage.Models;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MisguidedLogs.Gold.WarcraftLogs.Bunnycdn;

public class BunnyCdnStorageLoader(BunnyCDNStorage bunnyCDNStorage)
{
    public async Task<StorageObject[]> GetListOfStorageObjects(string path)
    {
        return [.. await bunnyCDNStorage.GetStorageObjectsAsync(path)];
    }
    
    public async Task<T?> GetStorageObject<T>(string fullPath)
    {
        var stream = await bunnyCDNStorage.DownloadObjectAsStreamAsync(fullPath);
        await using var decompressStream = new GZipStream(stream, CompressionMode.Decompress);
        return JsonSerializer.Deserialize<T>(decompressStream, new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } }) ?? default;
    }

    public async Task<T?> TryGetStorageObject<T>(string fullPath)
    {
        try
        {
            return await GetStorageObject<T>(fullPath);
        }
        catch (Exception)
        {
            return default;
        }
    }

}
