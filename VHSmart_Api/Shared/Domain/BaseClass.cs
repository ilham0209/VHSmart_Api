namespace VHSmart_Api.Shared.Domain;

public abstract class BaseClass
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string SysUserCreated { get; set; } = string.Empty;
    public DateTime SysDateCreated { get; set; }

    public string? SysUserModified { get; set; }
    public DateTime? SysDateModified { get; set; }

    public bool IsDeleted { get; set; }
}
