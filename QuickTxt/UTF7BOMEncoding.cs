using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickTxt
{    
    public class UTF7BOMEncoding : UTF7Encoding
    {
        // From Wikipedia:
        // ---------------
        // While a Unicode signature is typically a single, fixed byte sequence, the nature of UTF-7 necessitates 5 variations:
        // The last 2 bits of the 4th byte of the UTF-7 encoding of U + FEFF belong to the following character,
        // resulting in 4 possible bit patterns and therefore 4 different possible bytes in the 4th position.
        // The 5th variation is needed to disambiguate the case where no characters at all follow the signature.See the UTF - 7 entry in the table of Unicode signatures.

        /// <summary>
        /// Assumes the encoded string ONLY consists of a UTF-7 BOM with no actual content (e.g. an empty text file with a UTF-7 BOM prepended).
        /// </summary>
        /// <returns></returns>
        public override byte[] GetPreamble()
        {
            byte[] preamble = new byte[5] { 43, 47, 118, 56, 45 };

            return preamble;
        }

        /// <summary>
        /// UTF-7 REQUIRES the 1st character after the BOM to return what the actual BoM is, as such there are four possibilities.
        /// </summary>
        /// <param name="followingCharacter"></param>
        /// <returns></returns>
        public byte[] GetPreamble(char followingCharacter)
        {
            byte[] preambleChar = new UTF7Encoding().GetBytes("\uFEFF" + followingCharacter);

            byte[] preamble = preambleChar.Take(4).ToArray();

            return preamble;
        }

        /// <summary>
        /// HACK: Try and determine UTF7 BOM length, if present.
        /// </summary>
        /// <param name="bytes">This is the first four (at least) bytes of the text file byte array. Less will simply return 0 as this is not a valid UTF7 BOM.</param>
        /// <returns>BOM length.</returns>
        public static int GetUTF7PreambleLength(byte[] bytes)
        {
            int bomLength = 0; // If some bizarre logic that I've missed manifests, just assigned 0 for now...

            if (bytes.Length >= 4 && (bytes[0] == 43 && bytes[1] == 47 && bytes[2] == 118 && (bytes[3] == 56 || bytes[3] == 57 || bytes[3] == 58 || bytes[3] == 59)))
            {
                if (bytes.Length == 4) // The text file appears to only consist of the UTF7 BOM siginture of 4 bytes
                {
                    bomLength = 4;
                }
                else if (bytes.Length >= 5)
                {
                    if (bytes[4] == 45) // The fifth byte is a - which is closes the BoM signiture under most cases.
                    {
                        bomLength = 5;
                    }
                    else // The fifth byte is something else. Usually this means the text file is starting with a symbol that has to be encoded with UTF7 tags.
                    {
                        bomLength = 3;  // Should be 4, but we're going to re-use the 4th byte to fix a bug with .NET's UTF7 decoder.                        
                    }
                }
            }
            else // The first four bytes do not match the BOM signiture. This should be a very rare case the the detector tests the first three.                    
            {
                bomLength = 0;
            }

            return bomLength;
        }
    }   
}
