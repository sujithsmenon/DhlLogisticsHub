namespace DhlLogistics.Shared.Models;

public record GpsUpdateRequest(int JobId, double Lat, double Lng, double Speed);
