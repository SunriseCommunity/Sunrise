namespace Sunrise.Utils;

public static class Parsers
{
    //need to rewrite it ;>
    public static (string username, string passHash, string version, short utcOffset) ParseLogin(string strToParse)
    {
        var splittedString = strToParse.Split("\n");
        var strings = splittedString[2].Split("|");
        return (splittedString[0], splittedString[1], strings[0], short.Parse(strings[1]));
    }
}