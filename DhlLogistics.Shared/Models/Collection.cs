namespace DhlLogistics.Shared.Models;

public class Collection
{
    public int Id { get; set; }
    public DateTime CollectionDate { get; set; }
    public int? ContainerId { get; set; }
    public int? JobId { get; set; }

    public Container? Container { get; set; }
}
