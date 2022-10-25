using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Text;

namespace QuickTxt
{
    /// <summary>
    /// This is required as Fonts use two properties to set the text style / weight.
    /// However, in most utilities such as Notepad, this is exposed to the user as a single choice.
    /// Hence the need to create a "combined" class type.
    /// </summary>
    internal class FontStyleWeight
    {
        internal string Name { get; set; }
        internal FontStyle Style { get; set; }
        internal FontWeight Weight { get; set; }

        public FontStyleWeight(string name, FontStyle style, FontWeight weight)
        {
            Name = name;
            Style = style;
            Weight = weight;
        }

        /// <summary>
        /// Need to rethink if there is a cleaner way to do the below.
        /// </summary>
        /// <param name="style"></param>
        /// <param name="weight"></param>
        /// <returns></returns>
        public static FontStyleWeight GetFontStyleWeight(FontStyle style, FontWeight weight)
        {
            FontStyleWeight styleWeight = Constants.FONT_STYLES_WEIGHTS.Where(sw => sw.Style == style && sw.Weight.Weight == weight.Weight).FirstOrDefault();

            return styleWeight;
        }
    }
}
