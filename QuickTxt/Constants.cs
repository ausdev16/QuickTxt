using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Input.Spatial;
using Windows.UI.Text;

namespace QuickTxt
{    
    internal static class Constants
    {
        internal const int MIN_WIDTH = 421; // UWP Default 500
        internal const int MIN_HEIGHT = 132; // UWP Default 320

        // Random Note: A TextBox seems to convert all newlines to \r when you assign a string to it (ironically this the old mac stanard). This needs to converted back on save.
        internal const string CLASSIC_MACOS_NEWLINE = "\r";
        internal const string WINDOWS_NEWLINE = "\r\n";
        internal const string UNIX_NEWLINE = "\n";

        internal static readonly NewlineName[] NEWLINES = { new NewlineName(0, WINDOWS_NEWLINE), new NewlineName(1, UNIX_NEWLINE), new NewlineName(2, CLASSIC_MACOS_NEWLINE) };

        // UTF Code Pages. Used to determine whether or not a BOM is still required.
        internal static readonly int[] UTF_CODE_PAGES = { 65000, 65001, 1200, 1201, 12000, 12001 };
        // A constant version of the above is required for some code.
        internal const int UTF7_CODE_PAGE = 65000;
        internal const int UTF8_CODE_PAGE = 65001;
        internal const int UTF16LE_CODE_PAGE = 1200;
        internal const int UTF16BE_CODE_PAGE = 1201;
        internal const int UTF32LE_CODE_PAGE = 12000;
        internal const int UTF32BE_CODE_PAGE = 12001;

        // .NET Framework for the Windows Desktop supported Unicode and Code Page Encodings.
        // Maybe this is stored somewhere in the actual framework?
        internal static readonly int[] CODE_PAGES = { 37, 437, 500, 708, 720, 737, 775, 850, 852, 855, 857, 858, 860, 861, 862, 863, 864, 865, 866, 869, 870, 874, 875, 932, 936, 949, 950, 1026, 1047, 1140, 1141, 1142, 1143, 1144, 1145, 1146, 1147, 1148, 1149, 1200, 1201, 1250, 1251, 1252, 1253, 1254, 1255, 1256, 1257, 1258, 1361, 10000, 10001, 10002, 10003, 10004, 10005, 10006, 10007, 10008, 10010, 10017, 10021, 10029, 10079, 10081, 10082, 12000, 12001, 20000, 20001, 20002, 20003, 20004, 20005, 20105, 20106, 20107, 20108, 20127, 20261, 20269, 20273, 20277, 20278, 20280, 20284, 20285, 20290, 20297, 20420, 20423, 20424, 20833, 20838, 20866, 20871, 20880, 20905, 20924, 20932, 20936, 20949, 21025, 21866, 28591, 28592, 28593, 28594, 28595, 28596, 28597, 28598, 28599, 28603, 28605, 29001, 38598, 50220, 50221, 50222, 50225, 50227, 51932, 51936, 51949, 52936, 54936, 57002, 57003, 57004, 57005, 57006, 57007, 57008, 57009, 57010, 57011, 65000, 65001 };

        // Additional UTF Encodings without BOMs, plus custom UTF7 with BOM implementation
        internal static readonly Encoding[] ADDITIONAL_UTF_ENCODINGS = { new UTF7BOMEncoding(), new UTF8Encoding(false), new UnicodeEncoding(false, false), new UnicodeEncoding(true, false), new UTF32Encoding(false, false), new UTF32Encoding(true, false) };

        // English languages fully compatible with US ASCII
        internal static readonly string[] US_ASCII_LANGUAGES = { "en-AU", "en-NZ", "en-US", "en-CA" };

        internal static readonly string[] ISO_8859_1_LANGUAGES = { "af-ZA", "br-FR", "fo-FO", "ga-IE", "ms-BN", "ms-MY", "oc-FR", "rm-CH", "sw-KE", "sq-AL", "co-FR", "gl-ES", "id-ID", "pt-BR", "pt-PT", "sma-NO", "sv-SE", "sv-FI", "eu-ES", "is-IS", "it-IT", "it-CH", "lb-LU", "nb-NO", "nn-NO", "gd-GB", "en-IN", "en-IE", "en-ZA", "en-GB", "es-AR", "es-VE", "es-BO", "es-CL", "es-CO", "es-CR", "es-DO", "es-EC", "es-SV", "es-GT", "es-HN", "es-MX", "es-NI", "es-PA", "es-PY", "es-PE", "es-PR", "es-ES", "es-US", "es-UY", "en-BZ", "en-029", "en-IN", "en-IE", "en-JM", "en-MY", "en-PH", "en-SG", "en-ZA", "en-TT", "en-GB", "en-ZW" };
        
        // Default Font
        internal const string FONT_FAMILY = "Segoe UI";

        internal static readonly double[] FONT_SIZES = { 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 26, 29, 32, 35, 36, 37, 38, 40, 42, 45, 48 };

        // What appears as a font style is actually two font properties. Hence the below custom static objects.
        internal static readonly FontStyleWeight[] FONT_STYLES_WEIGHTS = { new FontStyleWeight("Regular", FontStyle.Normal, FontWeights.Normal), new FontStyleWeight("Italic", FontStyle.Italic, FontWeights.Normal), new FontStyleWeight("Bold", FontStyle.Normal, FontWeights.Bold), new FontStyleWeight("Bold Italic", FontStyle.Italic, FontWeights.Bold) };
    }
}
