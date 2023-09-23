using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Rijndael256;
using Rijndael = Rijndael256.Rijndael;

namespace Sunrise.Helpers;

public sealed class ScoreDecoder
{
    //I think it's better to put it in the "Score" class. No idea btw
    public static string Decode(string encodedString, string iv, string key, string osuVer)
    {
        //osu!-scoreburgr---------
        var keyConcatenated = key + osuVer;
        var keyBytes = Encoding.Default.GetBytes(keyConcatenated);
        
        var ivBytes = Convert.FromBase64String(iv);
        var encodedStrBytes = Convert.FromBase64String(encodedString);

        // Configure the engine
        var engine = new RijndaelEngine(256);
        
        var blockCipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(engine), new Pkcs7Padding()); 

        // Configure the parameters with the key and IV
        ICipherParameters keyParam = new KeyParameter(keyBytes);
        ICipherParameters parameters = new ParametersWithIV(keyParam, ivBytes);

        // Initialize the cipher for decryption
        blockCipher.Init(false, parameters);

        var outputBytes = new byte[blockCipher.GetOutputSize(encodedStrBytes.Length)];

        // Decrypt the cipher text
        var len = blockCipher.ProcessBytes(encodedStrBytes, 0, encodedStrBytes.Length, outputBytes, 0);
        len += blockCipher.DoFinal(outputBytes, len);

        // Resize the output array
        Array.Resize(ref outputBytes, len);

        // Get the plain text
        string plainText = Encoding.UTF8.GetString(outputBytes);

        Console.WriteLine(plainText);

        return plainText;
    }
}