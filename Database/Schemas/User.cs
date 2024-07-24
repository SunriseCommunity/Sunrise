using Sunrise.Types.Enums;
using Watson.ORM.Core;

namespace Sunrise.Database;

[Table("user")]
public class UserSchema
{
    [Column( true, DataTypes.Int, false)]
    public int Id { get; set; }
    
    [Column(  DataTypes.Nvarchar, 64, false)]
    public string Username { get; set; }
    
    [Column(  DataTypes.Nvarchar, 64, false)]
    public string Passhash { get; set; }
    
    [Column( DataTypes.Nvarchar, 64, false)]
    public string Token { get; set; }
    
    [Column( DataTypes.Int, false)]
    public short Country { get; set; }
    
    [Column( DataTypes.Int, false)]
    public UserPrivileges Privilege { get; set; }

    [Column(DataTypes.Decimal, maxLength: 100, precision: 2, false)]
    public float Accuracy { get; set; } = 0;
    
    [Column( DataTypes.Double, maxLength: 45, precision: 2, false)]
    public long TotalScore { get; set; } = 0;
    
    [Column(DataTypes.Double,  maxLength: 45, precision: 2, false)]
    public long RankedScore { get; set; } = 0;
    
    [Column( DataTypes.Int, false)]
    public int PlayCount { get; set; } = 0;
    
    [Column( DataTypes.Int, false)]
    public int PerformancePoints { get; set; } = 0;
    
    [Column( DataTypes.Int, false)]
    public int PlayTime { get; set; } = 0;
    
    [Column(DataTypes.DateTime, false)]
    public DateTime RegisterDate { get; set; } = DateTime.Now;
    
    public UserSchema()
    {
    }
    
    public UserSchema SetUserStats(string username, string passhash, string token, short country, UserPrivileges privilege)
    {
        Username = username;
        Passhash = passhash;
        Token = token;
        Country = country;
        Privilege = privilege;
        RegisterDate = DateTime.Now;
        return this;
    }

    public string UpdateToken()
    {
        Token = Guid.NewGuid().ToString();
        return Token;
    }
}