using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding;

namespace QuickTxt
{
    /// <summary>
    /// The main point of this class is to cut down on the code in MainPage.cs
    /// There is a most likely a better more object orientated way to do this if the app ever expands beyound it's current scope.
    /// </summary>
    internal static class Helpers
    {
        internal static string LastErrorMessage = "";

        /// <summary>
        /// The .NET Framework Class Library provides one static property, CodePagesEncodingProvider.Instance, that returns an EncodingProvider object that makes the full set of encodings available on the desktop .NET Framework Class Library available to .NET Core applications.
        /// </summary>
        /// <returns></returns>
        internal static bool RegisterCodePagesEncodingProvider()
        {
            bool success;

            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                success = true;
            }
            catch (Exception e)
            {
                LastErrorMessage = e.Message;

                success = false;
            }

            return success;
        }

        /// <summary>
        /// Returns the character set of the original IBM PC (personal computer). It is also known as CP437, OEM-US, OEM 437, PC-8, or DOS Latin US.
        /// If CodePagesEncodingProvider has not being registered, will return the next closest thing - plain ASCII.
        /// </summary>
        /// <returns></returns>
        internal static Encoding GetCodePage437()
        {
            Encoding encoding;

            try
            {
                encoding =  Encoding.GetEncoding(437);
            }
            catch (Exception e)
            {
                LastErrorMessage = e.Message;

                encoding = Encoding.ASCII;
            }

            return encoding;
        }

        /// <summary>
        /// Returns the current line/column of a cursor which is passed as a selection of the start of the document to the current cursors position.
        /// </summary>
        /// <param name="subString"></param>
        /// <returns></returns>
        internal static Tuple<int, int> GetLineCol(string subString)
        {            
            int lineCount = subString.Count(c => c == Constants.CLASSIC_MACOS_NEWLINE.SingleOrDefault()) + 1;
            int columnCount = subString.Length - subString.LastIndexOf(Constants.CLASSIC_MACOS_NEWLINE);

            return new Tuple<int, int>(lineCount, columnCount);
        }

        /// <summary>
        /// Saves text to a file. Will attempt to use UWP built in function if ASCII or Unicode, otherwise will convert to a byte array based on the current encoding and save in binary.
        /// </summary>
        /// <param name="textToSave"></param>
        /// <param name="encoding"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        internal static async Task<bool> WriteTextToFileAsync(String textToSave, Encoding encoding, StorageFile file, bool bom = false)
        {            
            try
            {
                if (encoding.CodePage == Encoding.ASCII.CodePage && !bom)
                {
                    await FileIO.WriteTextAsync(file, textToSave); // Seems to save as ASCII, OR UTF-8 if more bytes than 128. As UTF-8 is backwards compatible for the first 128 bytes with US-ASCII, quite possibly this is what it is using for both.
                }
                else if (encoding.CodePage == Encoding.UTF8.CodePage && bom)
                {
                    await FileIO.WriteTextAsync(file, textToSave, UnicodeEncoding.Utf8);
                }
                else if (encoding.CodePage == Encoding.Unicode.CodePage && bom)
                {
                    await FileIO.WriteTextAsync(file, textToSave, UnicodeEncoding.Utf16LE);
                }
                else if (encoding.CodePage == Encoding.BigEndianUnicode.CodePage && bom)
                {
                    await FileIO.WriteTextAsync(file, textToSave, UnicodeEncoding.Utf16BE);
                }
                else
                {                    
                    byte[] bytes;

                    if (bom && (encoding.CodePage != Encoding.UTF7.CodePage)) // This will account for UTF32LE/BE as they do not have thier own methods as per above.
                    {
                        byte[] bomBytes = encoding.GetPreamble();
                        byte[] buffer = encoding.GetBytes(textToSave);

                        bytes = new byte[bomBytes.Length + buffer.Length];

                        System.Buffer.BlockCopy(bomBytes, 0, bytes, 0, bomBytes.Length);
                        System.Buffer.BlockCopy(buffer, 0, bytes, bomBytes.Length, buffer.Length);
                    }
                    else if (bom && (encoding.CodePage == Encoding.UTF7.CodePage))
                    {
                        // Manually adds the UTF7 BOM
                        bytes = encoding.GetBytes("\uFEFF" + textToSave);
                    }
                    else //no BOM
                    {
                        bytes = encoding.GetBytes(textToSave);
                    }

                    await FileIO.WriteBytesAsync(file, bytes);
                }
            }
            catch (Exception e)
            {
                LastErrorMessage = e.Message;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Reads a text file in binary. It can be converted to text later based on the Encoding.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        internal static async Task<byte[]> ReadBytesFromFileAsync(StorageFile file)
        {
            // Read bytes from file
            byte[] bytes;
            try
            {
                IBuffer buffer = await FileIO.ReadBufferAsync(file);
                DataReader reader = DataReader.FromBuffer(buffer);
                bytes = new byte[reader.UnconsumedBufferLength];
                reader.ReadBytes(bytes);
            }
            catch (Exception e)
            {
                LastErrorMessage = e.Message;
                bytes = null;
            }

            return bytes;
        }            

        /// <summary>
        /// "Randomally" checks for newlines returning the most likely sequence. Could have issues with documents with multiple types of new lines, however, this should be a rare case so is not priority.
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="startIndex"></param>
        /// <returns></returns>
        // TO DO: Make this more robust. Check first new line, last new line and a small number in between. Also need to handle mangled txt files with multiple line breaks.
        internal static string DetectTextByteArrayNewLine(byte[] bytes, int startIndex = 0)
        {
            string newLine = Environment.NewLine; // Default

            // *** VERY IMPORTANT FOR SPEED ***
            // At this stage, the number of bytes to check is made up.
            // TO DO: Some form of intelligent selection
            int length = bytes.Length - startIndex;
            if (length == 0)
            {
                return newLine;
            }
            int bytesToScan = length > 100 ? 100 : length; // Made up magic number

            int numberOfTests = 7; // Again, a bit of a magic number
            
            string[] newLines = new string[numberOfTests];

            Random random = new Random();
            int firstOrRandomIndex;

            for (int i = 0; i < numberOfTests - 1; i++) // Reserve last index for last new line below.
            {
                if (i == 0)
                {
                    firstOrRandomIndex = startIndex;
                }
                else
                {
                    firstOrRandomIndex = random.Next(startIndex, length);
                    bytesToScan = firstOrRandomIndex + bytesToScan > length ? length - firstOrRandomIndex : bytesToScan;
                }

                newLines[i] = "";
                for (int j = firstOrRandomIndex; j < bytesToScan; j++)
                {
                    if (bytes[j] == 0x0a) // Unix newline
                    {
                        if (j - 1 >= 0) // Make sure we are not on the first byte
                        {
                            if (bytes[j - 1] == 0x0d) // Windows newline
                            {
                                newLines[i] = Constants.WINDOWS_NEWLINE;
                                break;
                            }
                            else // Some other characters precedes 0a, must be a Unix newline
                            {
                                newLines[i] = Constants.UNIX_NEWLINE.ToString();
                                break;
                            }
                        }
                        else // The first character is a Unix line break, it can't be Windows
                        {
                            newLines[i] = Constants.UNIX_NEWLINE.ToString();
                            break;
                        }
                    }
                    else if (bytes[j] == 0x0d) // Classic Mac OS newline. This must be on its own or it would have been found above.
                    {
                        newLines[i] = Constants.CLASSIC_MACOS_NEWLINE.ToString();
                    }
                }
            }
                        
            int lastIndex = numberOfTests - 1;
            for (int i = length - 1; i >= length - bytesToScan; i--)
            {
                if (bytes[i] == 0x0a) // Unix newline
                {
                    if (i - 1 >= 0) // Make sure we are not on the first byte
                    {
                        if (bytes[i - 1] == 0x0d) // Windows newline
                        {
                            newLines[lastIndex] = Constants.WINDOWS_NEWLINE;
                            break;
                        }
                        else // Some other characters precedes 0a, must be a Unix newline
                        {
                            newLines[lastIndex] = Constants.UNIX_NEWLINE.ToString();
                            break;
                        }
                    }
                    else // The first character is a Unix line break, it can't be Windows
                    {
                        newLines[lastIndex] = Constants.UNIX_NEWLINE.ToString();
                        break;
                    }
                }
                else if (bytes[i] == 0x0d) // Classic Mac OS newline. This must be on its own or it would have been found above.
                {
                    newLines[lastIndex] = Constants.CLASSIC_MACOS_NEWLINE.ToString();
                    break;
                }
            }

            bool match = false;
            string firstNewLine = "";
            for (int i = 0; i < numberOfTests; i++)
            {
                // Get a baseline to compare
                if (firstNewLine == "" && newLines[i] != "")
                {
                    firstNewLine = newLines[i];
                }
                else if (firstNewLine != "" && newLines[i] != "") // We have a first newline and the current line is not empty
                {
                    if (firstNewLine == newLines[i]) // The first newline matches the current value. Set match to true if not already
                    {
                        match = true;                        
                    }
                    else // THe first newline doesn't match the current newline. Set match to false and exit the loop - no point going further as we have a multiple newlines.
                    {
                        match = false;
                        break;
                    }
                }
                // Else the current new line is empty, so skip the comparison.
            }

            if (match)
            {
                newLine = firstNewLine;
            }
            // Else use the default as set at the start of this method

            return newLine;
        }

        /// <summary>
        /// Returns the character index for a specified line number (i.e. the start of the line).
        /// </summary>
        /// <param name="text"></param>
        /// <param name="lineNo"></param>
        /// <returns></returns>
        internal static int GetCursorPosition(string text, int lineNo)
        {
            int charNo = 0;
            int totalLines = text.Count(c => c == Constants.CLASSIC_MACOS_NEWLINE.SingleOrDefault()) + 1;

            if (lineNo <= 1)
            {
                charNo = 0;
            }
            else if (lineNo >= totalLines)
            {
                charNo = text.LastIndexOf(Constants.CLASSIC_MACOS_NEWLINE) + 1;
            }
            else
            {
                int lineCount = 1;
                int i = 0;
                while (i < text.Length)
                {
                    if (text[i] == Constants.CLASSIC_MACOS_NEWLINE.SingleOrDefault())
                    {
                        if (lineCount == lineNo)
                        {
                            charNo++;

                            break;
                        }

                        charNo = i;

                        lineCount++;
                    }

                    i++;
                }
            }

            return charNo;
        }
    }
}
