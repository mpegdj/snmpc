using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SnmpNms.Core.Models;
using SnmpNms.UI.Models;

namespace SnmpNms.UI.Services;

/// <summary>
/// Map 데이터를 JSON으로 저장/로드하는 서비스
/// </summary>
public class MapDataService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Map 데이터를 JSON 파일로 저장
    /// </summary>
    public void SaveToFile(MapNode rootSubnet, string filePath)
    {
        var data = new MapDataFile
        {
            Version = "1.0",
            CreatedAt = DateTime.Now,
            RootSubnet = ConvertToDto(rootSubnet)
        };

        var json = JsonSerializer.Serialize(data, _jsonOptions);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// JSON 파일에서 Map 데이터 로드
    /// </summary>
    public MapNode? LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        var json = File.ReadAllText(filePath);
        var data = JsonSerializer.Deserialize<MapDataFile>(json, _jsonOptions);

        if (data?.RootSubnet == null)
            return null;

        return ConvertFromDto(data.RootSubnet, null);
    }

    private MapNodeDto ConvertToDto(MapNode node)
    {
        var dto = new MapNodeDto
        {
            NodeType = node.NodeType,
            Name = node.Name,
            IsExpanded = node.IsExpanded,
            X = node.X,
            Y = node.Y
        };

        if (node.Target != null)
        {
            dto.Target = new SnmpTargetDto
            {
                IpAddress = node.Target.IpAddress,
                Port = node.Target.Port,
                Alias = node.Target.Alias,
                Device = node.Target.Device,
                Maker = node.Target.Maker,
                SysObjectId = node.Target.SysObjectId,
                Community = node.Target.Community,
                Version = node.Target.Version,
                Timeout = node.Target.Timeout,
                Retries = node.Target.Retries,
                PollingProtocol = node.Target.PollingProtocol
            };
        }

        foreach (var child in node.Children)
        {
            dto.Children.Add(ConvertToDto(child));
        }

        return dto;
    }

    private MapNode ConvertFromDto(MapNodeDto dto, MapNode? parent)
    {
        UiSnmpTarget? target = null;
        if (dto.Target != null)
        {
            target = new UiSnmpTarget
            {
                IpAddress = dto.Target.IpAddress,
                Port = dto.Target.Port,
                Alias = dto.Target.Alias ?? "",
                Device = dto.Target.Device ?? "",
                Maker = dto.Target.Maker ?? "",
                SysObjectId = dto.Target.SysObjectId ?? "",
                Community = dto.Target.Community ?? "public",
                Version = dto.Target.Version,
                Timeout = dto.Target.Timeout,
                Retries = dto.Target.Retries,
                PollingProtocol = dto.Target.PollingProtocol
            };
        }

        var node = new MapNode(dto.NodeType, dto.Name ?? "", target)
        {
            IsExpanded = dto.IsExpanded,
            X = dto.X,
            Y = dto.Y
        };

        foreach (var childDto in dto.Children)
        {
            var childNode = ConvertFromDto(childDto, node);
            node.AddChild(childNode);
        }

        return node;
    }
}

/// <summary>
/// Map 데이터 파일 구조
/// </summary>
public class MapDataFile
{
    public string Version { get; set; } = "1.0";
    public DateTime CreatedAt { get; set; }
    public MapNodeDto? RootSubnet { get; set; }
}

/// <summary>
/// MapNode의 직렬화용 DTO
/// </summary>
public class MapNodeDto
{
    public MapNodeType NodeType { get; set; }
    public string? Name { get; set; }
    public bool IsExpanded { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public SnmpTargetDto? Target { get; set; }
    public List<MapNodeDto> Children { get; set; } = new();
}

/// <summary>
/// UiSnmpTarget의 직렬화용 DTO
/// </summary>
public class SnmpTargetDto
{
    public string IpAddress { get; set; } = "";
    public int Port { get; set; } = 161;
    public string? Alias { get; set; }
    public string? Device { get; set; }
    public string? Maker { get; set; }
    public string? SysObjectId { get; set; }
    public string? Community { get; set; }
    public SnmpVersion Version { get; set; }
    public int Timeout { get; set; }
    public int Retries { get; set; }
    public PollingProtocol PollingProtocol { get; set; }
}

