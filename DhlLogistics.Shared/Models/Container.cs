namespace DhlLogistics.Shared.Models;

public class Container
{
    public int Id { get; set; }
    public string ContainerNumber { get; set; } = string.Empty;
    public string ContainerType { get; set; } = string.Empty;
    public ContainerStatus Status { get; set; } = ContainerStatus.Available;
}

public enum ContainerStatus { Available, InUse, Maintenance }
