namespace DeviceSimulator.Host;

/// <summary>
/// DTO for pack info response.
/// </summary>
public record PackInfoDto(
    string Name,
    string Vendor,
    string Protocol,
    string Version,
    string? Description,
    List<string> Supports
);

/// <summary>
/// DTO for device group response.
/// </summary>
public record DeviceGroupDto(
    string GroupId,
    string PackName,
    int DeviceCount,
    DateTime StartedAt,
    string Status
);

/// <summary>
/// DTO for start devices request.
/// </summary>
public record StartDevicesRequest(
    string PackName,
    int Count
);

/// <summary>
/// DTO for stop devices request.
/// </summary>
public record StopDevicesRequest(
    string GroupId
);

/// <summary>
/// DTO for generic API response.
/// </summary>
public record ApiResponse<T>(
    bool Success,
    T? Data,
    string? Error
);
