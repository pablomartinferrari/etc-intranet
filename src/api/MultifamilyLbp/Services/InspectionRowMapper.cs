using Intranet.Api.MultifamilyLbp.Data.Entities;
using Intranet.Api.MultifamilyLbp.Models;

namespace Intranet.Api.MultifamilyLbp.Services;

public static class InspectionRowMapper
{
    private const double LeadPositiveThreshold = 1.0;

    public static InspectionRowRecord FromDto(XrfReadingDto dto, Guid jobEntityId, Guid? batchId, string sourceFileName, string dataType)
    {
        var roomOrArea = string.Join(" ",
            new[] { dto.RoomType, dto.RoomNumber }.Where(s => !string.IsNullOrWhiteSpace(s)));

        return new InspectionRowRecord
        {
            Id = Guid.NewGuid(),
            JobEntityId = jobEntityId,
            UploadBatchId = batchId,
            ReadingId = dto.ReadingId,
            SourceFileName = sourceFileName,
            DataType = NormalizeDataType(dataType),
            Location = dto.Location,
            RoomOrArea = string.IsNullOrWhiteSpace(roomOrArea) ? null : roomOrArea,
            Component = dto.Component,
            NormalizedComponent = dto.NormalizedComponent,
            Substrate = dto.Substrate,
            NormalizedSubstrate = dto.NormalizedSubstrate,
            ShotCount = 1,
            Color = dto.Color,
            LeadContent = dto.LeadContent,
            IsPositive = dto.IsPositive || dto.LeadContent >= LeadPositiveThreshold,
            UnitNumber = dto.UnitNumber,
            Multifamily = dto.Multifamily,
            SiteAddress = dto.SiteAddress,
            RoomType = dto.RoomType,
            RoomNumber = dto.RoomNumber,
            Side = dto.Side,
            Condition = dto.Condition,
            Timestamp = dto.Timestamp,
            ValidationStatus = ValidateRow(dto),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    public static XrfReadingDto ToDto(InspectionRowRecord row) => new()
    {
        ReadingId = row.ReadingId,
        Component = row.Component,
        Color = row.Color,
        LeadContent = row.LeadContent,
        NormalizedComponent = row.NormalizedComponent,
        NormalizedSubstrate = row.NormalizedSubstrate,
        IsPositive = row.IsPositive,
        Location = row.Location,
        UnitNumber = row.UnitNumber,
        Multifamily = row.Multifamily,
        SiteAddress = row.SiteAddress,
        RoomType = row.RoomType,
        RoomNumber = row.RoomNumber,
        Substrate = row.Substrate,
        Side = row.Side,
        Condition = row.Condition,
        Timestamp = row.Timestamp,
        AreaType = row.DataType.Equals("commonAreas", StringComparison.OrdinalIgnoreCase)
            ? "Common Areas"
            : "Units",
    };

    public static string NormalizeDataType(string dataType)
    {
        if (dataType.Contains("common", StringComparison.OrdinalIgnoreCase))
            return "commonAreas";
        return "units";
    }

    private static string ValidateRow(XrfReadingDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Component))
            return "error";
        if (string.IsNullOrWhiteSpace(dto.Location))
            return "warning";
        return "clean";
    }
}
