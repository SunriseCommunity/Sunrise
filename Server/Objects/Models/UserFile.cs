using Sunrise.Server.Types.Enums;
using Watson.ORM.Core;

namespace Sunrise.Server.Objects.Models;

[Table("user_file")]
public class UserFile
{
    [Column(true, DataTypes.Int, false)]
    public int Id { get; set; }
    
    [Column(DataTypes.Int, false)]
    public int OwnerId { get; set; }
    
    [Column(DataTypes.Nvarchar, 64, false)]
    public string Path { get; set; }
    
    [Column(DataTypes.Int, false)]
    public FileType Type { get; set; }
    
    [Column(DataTypes.DateTime, false)]
    public DateTime CreatedAt { get; set; }
}
