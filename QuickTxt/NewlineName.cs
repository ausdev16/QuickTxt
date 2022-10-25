using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickTxt
{
    public class NewlineName
    {
        public int ID { get; set; }
        public string Newline { get; set; }
        public string FriendlyName { get; set; }

        public NewlineName()
        {            
            Newline = Environment.NewLine;
            ID = getID(Newline);
            FriendlyName = GetNewLineFriendlyName(Newline);
        }
        public NewlineName (string newline)
        {
            ID = getID(newline);
            Newline = newline;
            FriendlyName = GetNewLineFriendlyName(Newline);
        }

        public NewlineName (int id, string newline)
        {
            ID = id;
            Newline = newline;
            FriendlyName = GetNewLineFriendlyName(Newline);
        }

        private int getID(string newline)
        {
            int id;

            switch (newline)
            {
                case "\r\n":
                    id = 0;
                    break;
                case "\n":
                    id = 1;
                    break;
                case "\r":
                    id = 2;
                    break;
                default:
                    id = -1;
                    break;
            }

            return id;
        }

        /// <summary>
        /// Returns a user-friendly name for a new line sequence.
        /// </summary>
        /// <param name="newline"></param>
        /// <returns></returns>
        internal static String GetNewLineFriendlyName(string newline)
        {
            string friendlyName;

            switch (newline)
            {
                case "\r\n":
                    friendlyName = "Windows (CRLF)";
                    break;
                case "\n":
                    friendlyName = "Linux/macOS (LF)";
                    break;
                case "\r":
                    friendlyName = "Classic Mac OS (CR)";
                    break;
                default:
                    friendlyName = "Unknown Newline";                    
                    break;
            }

            return friendlyName;
        }

        internal static NewlineName GetNewlineName(string newline)
        {
            return new NewlineName(newline);
        }
    }
}
