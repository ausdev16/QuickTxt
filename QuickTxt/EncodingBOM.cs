using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickTxt
{
    /// <summary>
    /// This is a custom class to store the Encoding, and the BOM, along with some helper methods.
    /// </summary>
    public class EncodingBOM
    {
        public Encoding Encoding { get; private set; }     
        public int CodePage { get; private set; }

        public byte[] BOMBytes { get; private set; }
        public bool HasBOM { get; private set; }
        
        public string EncodingNameBOM { get; set; }

        public EncodingBOM(Encoding encoding)
        {
            Encoding = encoding;
            CodePage = encoding.CodePage;

            BOMBytes = encoding.GetPreamble();
            if (BOMBytes.Length > 0)
            {
                HasBOM = true;

                EncodingNameBOM = encoding.EncodingName + " BOM";
            }
            else
            {
                HasBOM = false;

                EncodingNameBOM = encoding.EncodingName;
            }
        }

        public static EncodingBOM GetEncodingBOM(int codePage)
        {
            return new EncodingBOM(Encoding.GetEncoding(codePage));
        }

        public static Encoding GetEncoding(int codePage, bool bom)
        {
            if (Constants.UTF_CODE_PAGES.Contains(codePage))
            {
                switch (codePage)
                {
                    case Constants.UTF7_CODE_PAGE: // UTF-7 work around as not supported natively
                        if (bom)
                        {
                            return new UTF7BOMEncoding();
                        }
                        else
                        {
                            return new UTF7Encoding();
                        }
                    case Constants.UTF8_CODE_PAGE:
                        return new UTF8Encoding(bom);
                    case Constants.UTF16LE_CODE_PAGE:
                        return new UnicodeEncoding(false, bom);
                    case Constants.UTF16BE_CODE_PAGE:
                        return new UnicodeEncoding(true, bom);
                    case Constants.UTF32LE_CODE_PAGE:
                        return new UTF32Encoding(false, bom);
                    case Constants.UTF32BE_CODE_PAGE:
                        return new UTF32Encoding(true, bom);
                    default: // Something went horribly wrong...
                        return Encoding.Default;
                }
            }
            else
            {
                return Encoding.GetEncoding(codePage);
            }
        }

        public static bool EncodingHasBOM(Encoding encoding)
        {
            bool hasBOM = encoding.GetPreamble().Length > 0;
            return hasBOM;
        }

        public static string GetEncodingNameBOM(Encoding encoding)
        {   
            if (EncodingHasBOM(encoding))
            {
                return encoding.EncodingName + " BOM";
            }
            else
            {
                return encoding.EncodingName;
            }
        }

        public static string GetEncodingNameBOM(Encoding encoding, bool bom)
        {
            if (bom)
            {
                return encoding.EncodingName + " BOM";
            }
            else
            {
                return encoding.EncodingName;
            }
        }
    }
}
