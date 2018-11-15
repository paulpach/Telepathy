using System.IO;
using System.Net.Sockets;

public static class NetworkStreamExtensions
{
    /// <summary>
    /// Reads the exactly amount bytes
    /// </summary>
    /// <returns>the specified amount of bytes or null if end of file</returns>
    /// <param name="stream">Stream.</param>
    /// <param name="amount">Amount.</param>
    /// <exception cref="System.IO.IOException">If there is any error</exception>
    public static byte[] ReadExactly(this Stream stream, int amount)
    {
        // there might not be enough bytes in the TCP buffer for .Read to read
        // the whole amount at once, so we need to keep trying until we have all
        // the bytes (blocking)
        //
        // note: this just is a faster version of reading one after another:
        //     for (int i = 0; i < amount; ++i)
        //         if (stream.Read(buffer, i, 1) == 0)
        //             return false;
        //     return true;
        byte[] buffer = new byte[amount];

        int bytesRead = 0;
        while (bytesRead < amount)
        {
            // read up to 'remaining' bytes with the 'safe' read extension
            int remaining = amount - bytesRead;
            int result = stream.Read(buffer, bytesRead, remaining);

            // .Read returns 0 if EOF
            if (result == 0)
                return null;

            // otherwise add to bytes read
            bytesRead += result;
        }
        return buffer;
    }
}
