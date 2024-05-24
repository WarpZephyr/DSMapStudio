using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudioCore.Utilities;
internal class DSMSEncoding
{
    public static readonly Encoding ASCII;

    public static readonly Encoding ShiftJIS;

    public static readonly Encoding UTF16;

    public static readonly Encoding UTF16BE;

    static DSMSEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ASCII = Encoding.ASCII;
        ShiftJIS = Encoding.GetEncoding("shift-jis");
        UTF16 = Encoding.Unicode;
        UTF16BE = Encoding.BigEndianUnicode;
    }
}
