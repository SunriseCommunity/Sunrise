using Microsoft.Data.Sqlite;

namespace Sunrise.Database.Sqlite;

public class SqliteDatabase
{
    private const string DatabasePath = "./Database/Sqlite/";
    private const string DatabaseName = "sunrise.db";

    private readonly SqliteConnection _connection = new($"Data Source={DatabasePath}{DatabaseName}");
    public readonly Files Files;
    public readonly Players Players;
    public readonly Scores Scores;

    public SqliteDatabase()
    {
        Console.WriteLine("Starting database");

        this.OpenDatabase();
        this.Files = new Files(this);
        this.Players = new Players(this);
        this.Scores = new Scores(this);

    }

    private void OpenDatabase()
    {
        _connection.Open();

        var schema = File.ReadAllText(DatabasePath + "schema.sql");
        var command = this.CreateCommand(schema);
        this.ExecuteNonQuery(command);
    }

    private void Open()
    {
        _connection.Open();
    }

    private void Close()
    {
        _connection.Close();
    }

    public SqliteCommand CreateCommand(string commandText)
    {
        return new SqliteCommand(commandText, _connection);
    }

    public SqliteDataReader ExecuteReader(SqliteCommand command)
    {
        return command.ExecuteReader();
    }

    public int ExecuteNonQuery(SqliteCommand command)
    {
        return command.ExecuteNonQuery();
    }
}