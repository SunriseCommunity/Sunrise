namespace Sunrise.Helpers;

public static class LoginParser
{
    //need to rewrite it ;>
    public static (string username, string passHash, string version, short utcOffset) Parse(string strToParse)
    {
        var splittedString = strToParse.Split("\n");
        var strings = splittedString[2].Split("|");
        Console.WriteLine(splittedString[0]);
        return (splittedString[0], splittedString[1], strings[0], short.Parse(strings[1]));
    }
}